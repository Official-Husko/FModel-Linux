namespace System.Windows;

/// <summary>
/// WPF teaches System.Uri the "pack://" scheme when it starts. Upstream builds such URIs directly
/// (new Uri("pack://application:,,,/Resources/...")), so the scheme has to be known here too;
/// <see cref="Application.GetResourceStream"/> then maps them onto Avalonia's avares:// resources.
/// </summary>
public static class PackUriScheme
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered || UriParser.IsKnownScheme("pack")) return;
        UriParser.Register(new GenericUriParser(GenericUriParserOptions.GenericAuthority), "pack", -1);
        _registered = true;
    }
}
