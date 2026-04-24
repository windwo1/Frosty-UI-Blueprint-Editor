using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UIBlueprintEditor.Editor.Text;
using UIBlueprintEditor.Editor.Textures;

namespace UIBlueprintEditor.Editor.UI
{
    public class ElementLoader
    {
        public static UIBlueprintElement LoadElement(dynamic uiElement, bool isWidget, Movement Movement, dynamic rootObject, Canvas parentCanvas, Action<dynamic, bool, Canvas> LoadUI)
        {
            bool createImages = Config.Get("RenderTextures", true);
            bool createWidgets = Config.Get("RenderWidgets", true);
            bool createText = Config.Get("RenderText", true);
            bool createFontEffects = Config.Get("RenderFontEffects", true);

            bool showAllUI = Config.Get("ShowAllUI", false);

            var elementName = uiElement.Internal.ToString();

            if ((elementName == "FrostySdk.Ebx.UIElementBitmapEntityData" || elementName == "FrostySdk.Ebx.PVZUIElementBitmapEntityData" || elementName == "FrostySdk.Ebx.PVZUIElementDynamicBitmapEntityData") && createImages)
            {
                try
                {
                    UIBlueprintElement element = new UIBlueprintElement(uiElement, isWidget, Movement, rootObject);

                    string textureMapId = uiElement.Internal.TextureId;

                    UIEditor.MappingIdToMapping.Remove(textureMapId);
                    UIEditor.MappingMinValue.Remove(textureMapId);
                    UIEditor.MappingMaxValue.Remove(textureMapId);
                    UIEditor.MappingTexture.Remove(textureMapId);

                    CreateTextures.GetTextures(rootObject, textureMapId);

                    double originalWidth = element.OriginalWidth;
                    double originalHeight = element.OriginalHeight;

                    var image = new Image
                    {
                        Width = element.Width,
                        Height = element.Height,
                        Stretch = Stretch.Fill,
                    };

                    try
                    {
                        image.Source = UIEditor.MappingTexture[textureMapId];

                        var uvRectFull = uiElement.Internal.UVRect;
                        Vector4 uvRect = new Vector4(uvRectFull.x, uvRectFull.y, uvRectFull.z, uvRectFull.w);

                        // these are multiplied by the width/height because min/max values start from 0 - 1
                        double minX = UIEditor.MappingMinValue[textureMapId].x * element.Width;
                        double minY = UIEditor.MappingMinValue[textureMapId].y * element.Height;
                        double maxX = UIEditor.MappingMaxValue[textureMapId].x * element.Width;
                        double maxY = UIEditor.MappingMaxValue[textureMapId].y * element.Height;

                        Point min = new Point(minX, minY);
                        Point max = new Point(maxX, maxY);

                        image.Clip = new RectangleGeometry(new Rect(min, max));
                        RenderOptions.SetBitmapScalingMode(image, bitmapScalingMode: BitmapScalingMode.Fant);

                        // scale up to previous size
                        double croppedWidth = maxX - minX;
                        double croppedHeight = maxY - minY;

                        double scaleX;
                        double scaleY;

                        // uses the original width/height so that if they are negative it should work fine
                        if (uvRect == new Vector4(1, 0, 0, 1))
                        {
                            // i dont really know what UVRect does but i know that UIs use a value
                            // of 1, 0, 0, 1 to horizontally flip stuff

                            scaleX = -originalWidth / croppedWidth;
                            scaleY = originalHeight / croppedHeight;

                            image.Margin = new Thickness(element.Width, 0, 0, 0);
                        }
                        else
                        {
                            scaleX = originalWidth / croppedWidth;
                            scaleY = originalHeight / croppedHeight;
                        }

                        var transformGroupImage = new TransformGroup();
                        transformGroupImage.Children.Add(new TranslateTransform(-minX, -minY));
                        transformGroupImage.Children.Add(new ScaleTransform(scaleX, scaleY));

                        image.RenderTransform = transformGroupImage;
                    }
                    catch (KeyNotFoundException)
                    {
                        BitmapImage texture = new BitmapImage();

                        texture.BeginInit();
                        texture.UriSource = new Uri("pack://application:,,,/UIBlueprintEditor;component/Images/Placeholder.png", UriKind.Absolute);
                        texture.EndInit();

                        image.Source = texture;
                    }

                    image.Opacity = uiElement.Internal.Visible ? element.Opacity : 0;
                    if (showAllUI) image.Opacity = 1;

                    parentCanvas.Children.Add(element);
                    element.Children.Add(image);

                    return element;
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"An error occurred while rendering the bitmap '{uiElement.Internal.InstanceName}': {ex}");

                    return null;
                }
            }
            else if ((elementName == "FrostySdk.Ebx.UIElementTextFieldEntityData" || elementName == "FrostySdk.Ebx.PVZUIElementTextFieldEntityData") && createText)
            {
                UIBlueprintElement element = new UIBlueprintElement(uiElement, isWidget, Movement, rootObject);

                // a border is used for setting a vertical text alignment later
                var border = new Border
                {
                    Width = element.Width,
                    Height = element.Height,
                };

                var tb = new TextBlock
                {
                };

                string sid = uiElement.Internal.Text.Sid;
                string fieldText = uiElement.Internal.FieldText;

                string outcome = sid == "" ? fieldText : sid;

                if (outcome != "")
                {
                    if (outcome.StartsWith("ID_"))
                    {
                        tb.Text = LocalizedStringDatabase.Current.GetString(outcome);
                    }
                    else
                    {
                        tb.Text = outcome;
                    }
                }
                else
                {
                    tb.Text = uiElement.Internal.InstanceName;
                }

                tb.Opacity = uiElement.Internal.Visible ? element.Opacity : 0;
                if (showAllUI) tb.Opacity = 1;

                float leftPadding = uiElement.Internal.AutoAdjustLeftPadding;
                float rightPadding = uiElement.Internal.AutoAdjustRightPadding;

                tb.Margin = new Thickness(leftPadding, 0, rightPadding, 0);

                if (uiElement.Internal.Password)
                {
                    tb.Text = new string('*', tb.Text.Length);
                }
                if (uiElement.Internal.Text.Wordwrap)
                {
                    tb.TextWrapping = TextWrapping.Wrap;
                }

                if (((PointerRef)uiElement.Internal.FontStyle).Type != PointerRefType.Null)
                {
                    var fontGuid = ((PointerRef)uiElement.Internal.FontStyle).External.FileGuid;
                    var fontEbx = App.AssetManager.GetEbxEntry(fontGuid);

                    EbxAsset fontAsset = App.AssetManager.GetEbx(fontEbx);
                    dynamic rootObjectFont = fontAsset.RootObject;

                    var fontEbxPath = rootObjectFont.Hd.Internal.FontLookup[0].FontAssetPath;

                    var fontEbxTTF = App.AssetManager.GetEbx(fontEbxPath);
                    ulong ttfRes = fontEbxTTF.RootObject.FontResource;

                    ResAssetEntry ttfResEntry = App.AssetManager.GetResEntry(ttfRes);

                    using (Stream ttfStream = App.AssetManager.GetRes(ttfResEntry))
                    {
                        string fontName = "./#" + fontEbxTTF.RootObject.FontFamilyName;

                        // 'HouseofTerror' font has a space for some reason
                        if (fontName == "./#MonsterFonts-HouseofTerror")
                        {
                            fontName = "./#MonsterFonts HouseofTerror";
                        }

                        string tempFile = Path.Combine(Path.GetTempPath(),
                            string.Format("{0:X16}.ttf", fontEbxTTF.RootObject.FontResource));

                        if (!File.Exists(tempFile))
                        {
                            using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                            {
                                ttfStream.CopyTo(fs);
                            }
                        }

                        tb.FontFamily = new FontFamily(new Uri(tempFile, UriKind.Absolute), fontName);
                    }

                    tb.FontSize = (double)rootObjectFont.Hd.Internal.PointSize;
                }
                else
                {
                    tb.Visibility = Visibility.Hidden;
                }

				switch (uiElement.Internal.Text.VerticalAlignment.ToString())
				{
					case "UIElementAlignment_Top":
						tb.VerticalAlignment = VerticalAlignment.Top;
						break;
					case "UIElementAlignment_Center":
						tb.VerticalAlignment = VerticalAlignment.Center;
						break;
					case "UIElementAlignment_Bottom":
						tb.VerticalAlignment = VerticalAlignment.Bottom;
						break;
					default:
						tb.VerticalAlignment = VerticalAlignment.Top;
						break;
				}

				// they spelt horizontal wrong lol
				switch (uiElement.Internal.Text.HorizonalAlignment.ToString())
                {
                    case "UIElementAlignment_Left":
                        tb.TextAlignment = TextAlignment.Left;
                        break;
                    case "UIElementAlignment_Center":
                        tb.TextAlignment = TextAlignment.Center;
                        break;
                    case "UIElementAlignment_Right":
                        tb.TextAlignment = TextAlignment.Right;
                        break;
                    default:
                        tb.TextAlignment = TextAlignment.Center;
                        break;
                }
                
                try
                {
                    if (createFontEffects)
                    {
                        var fontEffectGuid = ((PointerRef)uiElement.Internal.FontEffect).External.FileGuid;
                        var fontEffectEbx = App.AssetManager.GetEbxEntry(fontEffectGuid);

                        FontEffect fontEffect = new FontEffect();

                        fontEffect.Apply(tb, border, element, fontEffectEbx);
                    }
                }
                catch (NullReferenceException) { }

                parentCanvas.Children.Add(element);
                element.Children.Add(border);
                border.Child = tb;

                return element;
            }
            else if (elementName == "FrostySdk.Ebx.UIElementFillEntityData" || elementName == "FrostySdk.Ebx.PVZUIElementFillEntityData")
            {
                UIBlueprintElement element = new UIBlueprintElement(uiElement, isWidget, Movement, rootObject);

                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = element.Width,
                    Height = element.Height,
                };

                try
                {
                    var fillGuid = ((PointerRef)uiElement.Internal.Style).External.FileGuid;
                    var fillEbx = App.AssetManager.GetEbxEntry(fillGuid);

                    EbxAsset fillAsset = App.AssetManager.GetEbx(fillEbx);
                    dynamic rootObjectFill = fillAsset.RootObject;

                    var alpha = (float)rootObjectFill.BackgroundColor.Alpha;

                    var colorR = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.x * 255);
                    var colorG = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.y * 255);
                    var colorB = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.z * 255);

                    rect.Fill = new SolidColorBrush(Color.FromRgb(colorR, colorG, colorB));
                    rect.Opacity = alpha;
                }
                catch (NullReferenceException)
                {
                    rect.Fill = Brushes.White;
                }

                parentCanvas.Children.Add(element);
                element.Children.Add(rect);

                return element;
            }
            else if (elementName == "FrostySdk.Ebx.UIElementButtonEntityData")
            {
                bool showHitbox = Config.Get<bool>("ShowButtonHitboxes", false);

                UIBlueprintElement element = new UIBlueprintElement(uiElement, !showHitbox, Movement, rootObject);

                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = element.Width,
                    Height = element.Height,
                    Fill = Brushes.LightBlue,
                    Opacity = 0.25,
                };

                if (showHitbox)
                {
                    parentCanvas.Children.Add(element);
                    element.Children.Add(rect);
                }

                return element;
            }
            else if (elementName == "FrostySdk.Ebx.UIElementWidgetReferenceEntityData" && createWidgets)
            {
                UIBlueprintElement element = new UIBlueprintElement(uiElement, isWidget, Movement, rootObject);

                try
                {
                    var widgetGuid = ((PointerRef)uiElement.Internal.Blueprint).External.FileGuid;
                    var widgetEbx = App.AssetManager.GetEbxEntry(widgetGuid);

                    EbxAsset widgetAsset = App.AssetManager.GetEbx(widgetEbx);
                    dynamic rootObjectWidget = widgetAsset.RootObject;

                    var widgetSize = rootObjectWidget.Object.Internal.Size;

                    if (!uiElement.Internal.UseElementSize)
                    {
                        element.Width = widgetSize.X;
                        element.Height = widgetSize.Y;
                    }
                    else
                    {
                        element.Width = uiElement.Internal.Size.X;
                        element.Height = uiElement.Internal.Size.Y;
                    }

                    double offsetX = uiElement.Internal.Offset.X;
                    double offsetY = uiElement.Internal.Offset.Y;
                    double anchorX = uiElement.Internal.Anchor.X;
                    double anchorY = uiElement.Internal.Anchor.Y;

                    var mainSize = rootObject.Object.Internal.Size;

                    double widgetFinalX = anchorX * (mainSize.X - widgetSize.X) + offsetX;
                    double widgetFinalY = anchorY * (mainSize.Y - widgetSize.Y) + offsetY;

                    element.Opacity = element.Opacity;

                    Canvas.SetLeft(element, widgetFinalX);
                    Canvas.SetTop(element, widgetFinalY);

                    parentCanvas.Children.Add(element);

                    LoadUI(rootObjectWidget, true, element);
                }
                catch (NullReferenceException)
                {
                    // if there is no blueprint referenced, it'll just use a placeholder
                    var rect = new System.Windows.Shapes.Rectangle
                    {
                        Width = element.Width,
                        Height = element.Height,
                        Fill = Brushes.Orange,
                        Opacity = 0.05,
                    };

                    var tb = new TextBlock
                    {
                        Text = uiElement.Internal.InstanceName,
                        FontSize = 24,
                        Opacity = 0.2,
                    };

                    parentCanvas.Children.Add(element);
                    element.Children.Add(rect);
                    element.Children.Add(tb);
                }

                return element;
            }
            else
            {
                App.Logger.Log("Unrecognized UI element");

                UIBlueprintElement element = new UIBlueprintElement(uiElement, isWidget, Movement, rootObject);

                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = element.Width,
                    Height = element.Height,
                    Fill = Brushes.Orange,
                    Opacity = 0.05,
                };

                var tb = new TextBlock
                {
                    Text = uiElement.Internal.InstanceName,
                    FontSize = 24,
                    Opacity = 0.2,
                };

                parentCanvas.Children.Add(element);
                element.Children.Add(rect);
                element.Children.Add(tb);

                return element;
            }
        }

        public static void LoadList(dynamic rootObject, Canvas parentCanvas, Movement movement, Action<dynamic, bool, Canvas> LoadUI)
        {
            string incButton = rootObject.Object.Internal.IncreaseIndexButton.ToString();

            bool isVertical = incButton != "UIInputAction_NavigateRight" && incButton != "UIInputAction_TabRight";

            List<Guid> rows = new List<Guid>();

            if (rootObject.Object.Internal.DynamicRow_Template.Type != PointerRefType.Null)
            {
                int defaultCount = Config.Get("DefaultRowCount", 1);

                int dynamicCount = rootObject.Object.Internal.DynamicRowCount;
                int count = dynamicCount == 0 ? defaultCount : dynamicCount;

                for (int i = 0; i < count; i++)
                {
                    rows.Add(((PointerRef)rootObject.Object.Internal.DynamicRow_Template).External.FileGuid);
                }
            }
            else
            {
                for (int i = 0; i < rootObject.Object.Internal.Rows.Count; i++)
                {
                    var row = rootObject.Object.Internal.Rows[i];
                    rows.Add(((PointerRef)row.Internal.RowTemplate).External.FileGuid);
                }
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var widgetGuid = rows[i];
                var widgetEbx = App.AssetManager.GetEbxEntry(widgetGuid);

                EbxAsset widgetAsset = App.AssetManager.GetEbx(widgetEbx);
                dynamic rootObjectWidget = widgetAsset.RootObject;

                foreach (var layer in rootObjectWidget.Object.Internal.Layers)
                {
                    foreach (var uiElement in layer.Internal.Elements)
                    {
                        if (layer.Internal.Visible || Config.Get("ShowAllUI", false))
                        {
                            UIBlueprintElement canvas = LoadElement(uiElement, true, movement, rootObjectWidget, parentCanvas, LoadUI);

                            if (canvas == null)
                                return;

                            TransformGroup transformGroupRow = new TransformGroup();

                            var offsetX = rootObjectWidget.Object.Internal.Size.X * i;
                            var offsetY = rootObjectWidget.Object.Internal.Size.Y * i;
                            TranslateTransform translateTransformRow = new TranslateTransform(isVertical ? 0 : offsetX, isVertical ? offsetY : 0);

                            if (canvas.RenderTransform == null)
                            {
                                canvas.RenderTransform = transformGroupRow;
                            }

                            transformGroupRow = (TransformGroup)canvas.RenderTransform;
                            transformGroupRow.Children.Add(translateTransformRow);
                        }
                    }
                }
            }
        }
    }
}
