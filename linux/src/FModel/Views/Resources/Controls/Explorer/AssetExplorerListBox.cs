using System;
using Avalonia;
using Avalonia.Controls;

namespace FModel.Views.Resources.Controls.Explorer;

/// <summary>ListBox used by the assets explorer (tiled and list views). Lazily resolves the visible GameFileViewModels
/// (see <see cref="AssetExplorerBehavior"/>), opens files/folders on double click and shows FolderContextMenu on folders.</summary>
public class AssetExplorerListBox : ListBox
{
    protected override Type StyleKeyOverride => typeof(ListBox);

    public AssetExplorerListBox()
    {
        AssetExplorerBehavior.SetResolveVisibleItems(this, true);
        AssetExplorerBehavior.SetIsExplorer(this, true);
        AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
    }

    /// <summary>Calls GameFileViewModel.OnIsVisible() for every item currently shown in the viewport.</summary>
    public void RefreshVisibleItems() => AssetExplorerBehavior.RefreshVisibleItems(this);

    // Same as upstream: after scrolling down, changing folder or filtering can leave the offset past the end of
    // the (now shorter) list, which shows no items. Clamp it instead of jumping back to the top.
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.Source is ScrollViewer scroll && scroll.Offset.Y > scroll.ScrollBarMaximum.Y)
        {
            scroll.Offset = new Vector(scroll.Offset.X, scroll.ScrollBarMaximum.Y);
        }
    }
}
