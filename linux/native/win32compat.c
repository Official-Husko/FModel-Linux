/*
 * win32compat: the handful of Win32 functions FModel and Snooper P/Invoke, implemented for Linux.
 *
 * FModel.Linux routes DllImports of kernel32.dll, user32.dll and winbrand.dll to this library (see
 * linux/src/FModel.Linux/NativeCompat.cs), so the shared sources keep their Windows code paths and
 * still get sensible answers on Linux instead of a DllNotFoundException.
 *
 * Only libc and libdl are linked. X11/XRandR are loaded at runtime when available.
 */

#define _GNU_SOURCE
#include <dlfcn.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/sysinfo.h>
#include <uchar.h>

#define EXPORT __attribute__((visibility("default")))

/* ---- kernel32 ---------------------------------------------------------------------------------- */

/* Linux processes inherit the terminal they were started from; nothing to attach. */
EXPORT int AttachConsole(int process_id)
{
    (void) process_id;
    return 1;
}

typedef struct
{
    uint32_t Length;
    uint32_t MemoryLoad;
    uint64_t TotalPhys;
    uint64_t AvailPhys;
    uint64_t TotalPageFile;
    uint64_t AvailPageFile;
    uint64_t TotalVirtual;
    uint64_t AvailVirtual;
    uint64_t AvailExtendedVirtual;
} MEMORYSTATUSEX;

static uint64_t meminfo_kib(const char *key)
{
    FILE *file = fopen("/proc/meminfo", "r");
    if (!file) return 0;

    char line[256];
    uint64_t value = 0;
    size_t key_length = strlen(key);
    while (fgets(line, sizeof line, file))
    {
        if (strncmp(line, key, key_length) == 0 && line[key_length] == ':')
        {
            value = strtoull(line + key_length + 1, NULL, 10);
            break;
        }
    }

    fclose(file);
    return value;
}

EXPORT int GlobalMemoryStatusEx(MEMORYSTATUSEX *status)
{
    struct sysinfo info;
    if (!status || sysinfo(&info) != 0) return 0;

    uint64_t unit = info.mem_unit ? info.mem_unit : 1;
    uint64_t available = meminfo_kib("MemAvailable") * 1024;
    if (!available) available = (uint64_t) (info.freeram + info.bufferram) * unit;

    status->TotalPhys = (uint64_t) info.totalram * unit;
    status->AvailPhys = available;
    status->TotalPageFile = (uint64_t) (info.totalram + info.totalswap) * unit;
    status->AvailPageFile = available + (uint64_t) info.freeswap * unit;
    status->TotalVirtual = status->TotalPageFile;
    status->AvailVirtual = status->AvailPageFile;
    status->AvailExtendedVirtual = 0;
    status->MemoryLoad = status->TotalPhys ? (uint32_t) (100 - available * 100 / status->TotalPhys) : 0;
    return 1;
}

/* ---- user32 ------------------------------------------------------------------------------------ */

/* DEVMODEA as FModel marshals it (ANSI, sequential, natural alignment). */
typedef struct
{
    char dmDeviceName[32];
    int16_t dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
    int32_t dmFields;
    int32_t dmPositionX, dmPositionY;
    int32_t dmDisplayOrientation;
    int32_t dmDisplayFixedOutput;
    int16_t dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
    char dmFormName[32];
    int16_t dmLogPixels;
    int32_t dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
    int32_t dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
} DEVMODEA;

/* Minimal XRandR ABI (stable since 1.3), so no X11 development headers are needed to build this. */
typedef unsigned long XID;
typedef struct
{
    XID id;
    unsigned int width, height;
    unsigned long dotClock;
    unsigned int hSyncStart, hSyncEnd, hTotal, hSkew;
    unsigned int vSyncStart, vSyncEnd, vTotal;
    char *name;
    unsigned int nameLength;
    unsigned long modeFlags;
} XRRModeInfo;

typedef struct
{
    unsigned long timestamp, configTimestamp;
    int ncrtc;
    XID *crtcs;
    int noutput;
    XID *outputs;
    int nmode;
    XRRModeInfo *modes;
} XRRScreenResources;

typedef struct
{
    unsigned long timestamp;
    int x, y;
    unsigned int width, height;
    XID mode;
    unsigned short rotation;
    int noutput;
    XID *outputs;
    unsigned short rotations;
    int npossible;
    XID *possible;
} XRRCrtcInfo;

#define RR_INTERLACE 0x10
#define RR_DOUBLESCAN 0x20
#define MAX_RATES 16

static int rates[MAX_RATES];
static int rate_count = -1;

static void query_refresh_rates(void)
{
    rate_count = 0;
    if (!getenv("DISPLAY")) return;

    void *x11 = dlopen("libX11.so.6", RTLD_LAZY);
    void *xrandr = dlopen("libXrandr.so.2", RTLD_LAZY);
    if (!x11 || !xrandr) return;

    void *(*open_display)(const char *) = dlsym(x11, "XOpenDisplay");
    int (*close_display)(void *) = dlsym(x11, "XCloseDisplay");
    XID (*default_root)(void *) = dlsym(x11, "XDefaultRootWindow");
    XRRScreenResources *(*get_resources)(void *, XID) = dlsym(xrandr, "XRRGetScreenResourcesCurrent");
    void (*free_resources)(XRRScreenResources *) = dlsym(xrandr, "XRRFreeScreenResources");
    XRRCrtcInfo *(*get_crtc)(void *, XRRScreenResources *, XID) = dlsym(xrandr, "XRRGetCrtcInfo");
    void (*free_crtc)(XRRCrtcInfo *) = dlsym(xrandr, "XRRFreeCrtcInfo");
    if (!open_display || !close_display || !default_root || !get_resources || !free_resources || !get_crtc || !free_crtc) return;

    void *display = open_display(NULL);
    if (!display) return;

    XRRScreenResources *resources = get_resources(display, default_root(display));
    for (int c = 0; resources && c < resources->ncrtc && rate_count < MAX_RATES; c++)
    {
        XRRCrtcInfo *crtc = get_crtc(display, resources, resources->crtcs[c]);
        if (!crtc) continue;

        for (int m = 0; crtc->mode && m < resources->nmode; m++)
        {
            XRRModeInfo *mode = &resources->modes[m];
            if (mode->id != crtc->mode || !mode->hTotal || !mode->vTotal) continue;

            double vtotal = mode->vTotal;
            if (mode->modeFlags & RR_DOUBLESCAN) vtotal *= 2;
            if (mode->modeFlags & RR_INTERLACE) vtotal /= 2;
            rates[rate_count++] = (int) (mode->dotClock / (mode->hTotal * vtotal) + 0.5);
            break;
        }

        free_crtc(crtc);
    }

    if (resources) free_resources(resources);
    close_display(display);
}

/* Enumerates one "mode" per active monitor, carrying its current refresh rate. */
EXPORT int EnumDisplaySettings(const char *device, int mode_index, DEVMODEA *mode)
{
    (void) device;
    if (rate_count < 0) query_refresh_rates();
    if (!mode || mode_index < 0 || mode_index >= rate_count) return 0;

    mode->dmDisplayFrequency = rates[mode_index];
    return 1;
}

/* ---- winbrand ---------------------------------------------------------------------------------- */

/* Returns the distribution name (PRETTY_NAME from os-release) for FModel's log header. */
EXPORT char16_t *BrandingFormatString(const char16_t *format)
{
    (void) format;
    char name[256] = "Linux";

    FILE *file = fopen("/etc/os-release", "r");
    if (!file) file = fopen("/usr/lib/os-release", "r");
    if (file)
    {
        char line[512];
        while (fgets(line, sizeof line, file))
        {
            if (strncmp(line, "PRETTY_NAME=", 12) != 0) continue;
            char *value = line + 12;
            value[strcspn(value, "\r\n")] = 0;
            size_t length = strlen(value);
            if (length >= 2 && (value[0] == '"' || value[0] == '\'') && value[length - 1] == value[0])
            {
                value[length - 1] = 0;
                value++;
            }
            snprintf(name, sizeof name, "%s", value);
            break;
        }
        fclose(file);
    }

    /* the marshaller frees the result with free(); ASCII is plenty for a distribution name */
    size_t length = strlen(name);
    char16_t *result = malloc((length + 1) * sizeof(char16_t));
    if (!result) return NULL;
    for (size_t i = 0; i < length; i++) result[i] = (unsigned char) name[i] < 0x80 ? (char16_t) name[i] : u'?';
    result[length] = 0;
    return result;
}
