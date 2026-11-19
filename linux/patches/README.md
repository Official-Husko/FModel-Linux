# Submodule patch queue

`CUE4Parse/` and `Snooper/` are the official submodules, pinned to whatever commit upstream FModel
pins. When one of them needs a Linux fix that hasn't landed upstream yet, put it here as a regular
`git format-patch` file:

```
linux/patches/CUE4Parse/0001-short-description.patch
linux/patches/Snooper/0001-short-description.patch
```

`linux/scripts/apply-patches.sh` (run by every build script) applies them in name order and skips the
ones already applied. When upstream picks up a fix, or changes the surrounding code so the patch no
longer applies, the build stops and tells you which one to drop or refresh.

Prefer sending the fix to the submodule's own repository too; the goal is for this folder to be empty.
