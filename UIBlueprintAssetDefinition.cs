using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk.Interfaces;
using System;
using System.Windows.Media;

namespace UIBlueprintEditor
{
    public class UIBlueprintAssetDefinition : AssetDefinition
    {
        public static readonly ImageSource IconImage = new ImageSourceConverter().ConvertFromString("pack://application:,,,/UIBlueprintEditor;component/Images/UITypeIcon.png") as ImageSource;
        protected static ImageSource Icon => IconImage;
        public override ImageSource GetIcon()
        {
            return Icon;
        }

        public override FrostyAssetEditor GetEditor(ILogger logger)
        {
            return new UIEditor(logger);
        }
    }
}