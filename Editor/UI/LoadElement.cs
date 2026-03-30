using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UIBlueprintEditor.Editor.Text;
using UIBlueprintEditor.Editor.Textures;

namespace UIBlueprintEditor.Editor.UI
{
    public class LoadElement
    {
        private static bool debugging = UIEditor.debugging;

        // loads any single element
        public static UIBlueprintElement Load(dynamic uiComponent, bool isWidget, Movement Movement, dynamic rootObject, Canvas parentCanvas, Action<EbxAssetEntry, bool, Canvas> LoadUI)
        {
            // some settings that can be customized
            bool createImages = Config.Get("RenderTextures", true);
            bool createWidgets = Config.Get("RenderWidgets", true);
            bool createText = Config.Get("RenderText", true);
            bool createFontEffects = Config.Get("RenderFontEffects", true);

            bool showAllUI = Config.Get("ShowAllUI", false);

            var componentName = uiComponent.Internal.ToString();

            if ((componentName == "FrostySdk.Ebx.UIElementBitmapEntityData" || componentName == "FrostySdk.Ebx.PVZUIElementBitmapEntityData" || componentName == "FrostySdk.Ebx.PVZUIElementDynamicBitmapEntityData") && createImages)
            {
                try
                {
                    if (uiComponent.Internal.Visible || showAllUI)
                    {
                        UIBlueprintElement element = new UIBlueprintElement(uiComponent, isWidget, Movement);

                        string textureMapId = uiComponent.Internal.TextureId;

                        // gets all the textures needed for this bitmap
                        CreateTextures.GetTextures(rootObject, textureMapId);

                        // for storing the negative versions
                        double actualWidth = element.actualWidth;
                        double actualHeight = element.actualHeight;

                        var image = new Image
                        {
                            Width = element.Width,
                            Height = element.Height,
                            Stretch = Stretch.Fill,
                        };

                        // gets the needed texture from the dictionary created earlier with the texture map id as the key
                        var texture = UIEditor.mappingTexture[textureMapId];

                        // sets the source of the texture to the exported texture
                        image.Source = texture;

                        var uvRectFull = uiComponent.Internal.UVRect;
                        Vector4 uvRect = new Vector4(uvRectFull.x, uvRectFull.y, uvRectFull.z, uvRectFull.w);

                        // all the values needed for cropping a bitmap
                        // they are multiplied by the width/height because min/max values start from 0 - 1
                        double minX = UIEditor.mappingMinValue[textureMapId].x * element.Width;
                        double minY = UIEditor.mappingMinValue[textureMapId].y * element.Height;
                        double maxX = UIEditor.mappingMaxValue[textureMapId].x * element.Width;
                        double maxY = UIEditor.mappingMaxValue[textureMapId].y * element.Height;

                        Point min = new Point(minX, minY);
                        Point max = new Point(maxX, maxY);

                        // Clip is used to crop the texture
                        image.Clip = new RectangleGeometry(new Rect(min, max));
                        RenderOptions.SetBitmapScalingMode(image, bitmapScalingMode: BitmapScalingMode.Fant);

                        // scale up to previous size
                        double croppedWidth = maxX - minX;
                        double croppedHeight = maxY - minY;

                        double scaleX;
                        double scaleY;

                        // uses the actual width/height so that if they are negative it should work fine
                        if (uvRect == new Vector4(1, 0, 0, 1))
                        {
                            // i dont really know what UVRect does but i know that UIs use a value
                            // of 1, 0, 0, 1 to horizontally flip stuff

                            scaleX = -actualWidth / croppedWidth;
                            scaleY = actualHeight / croppedHeight;

                            // sets it back where it was
                            image.Margin = new Thickness(element.Width, 0, 0, 0);
                        }
                        else
                        {
                            scaleX = actualWidth / croppedWidth;
                            scaleY = actualHeight / croppedHeight;
                        }

                        var transformGroupImage = new TransformGroup();
                        transformGroupImage.Children.Add(new TranslateTransform(-minX, -minY));
                        transformGroupImage.Children.Add(new ScaleTransform(scaleX, scaleY));

                        image.RenderTransform = transformGroupImage;

                        image.Opacity = element.Opacity;

                        parentCanvas.Children.Add(element);
                        element.Children.Add(image);

                        return element;
                    }
                }
                catch (KeyNotFoundException)
                {
                    App.Logger.LogError($"The texture '{uiComponent.Internal.TextureId}' wasn't found in '{uiComponent.Internal.InstanceName}'");
                    // most of the time this is just caused by dynamic bitmaps which change their texture id when in game

                    return null;
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"An error occurred while rendering the bitmap '{uiComponent.Internal.InstanceName}': {ex}");

                    return null;
                }
            }
            else if ((componentName == "FrostySdk.Ebx.UIElementTextFieldEntityData" || componentName == "FrostySdk.Ebx.PVZUIElementTextFieldEntityData") && createText)
            {
                if (uiComponent.Internal.Visible || showAllUI)
                {
                    UIBlueprintElement element = new UIBlueprintElement(uiComponent, isWidget, Movement);

                    // a border is used for setting a vertical text alignment later
                    var border = new Border
                    {
                        Width = element.Width,
                        Height = element.Height,
                    };

                    var tb = new TextBlock
                    {
                    };

                    string sid = uiComponent.Internal.Text.Sid;
                    string fieldText = uiComponent.Internal.FieldText;

                    // some text fields use FieldText
                    string outcome = sid == "" ? fieldText : sid;

                    // font style
                    var fontGuid = ((PointerRef)uiComponent.Internal.FontStyle).External.FileGuid;
                    var fontEbx = App.AssetManager.GetEbxEntry(fontGuid);

                    EbxAsset fontAsset = App.AssetManager.GetEbx(fontEbx);
                    dynamic rootObjectFont = fontAsset.RootObject;

                    if (outcome != "")
                    {
                        // if its an id it will use the string of the id
                        if (outcome.StartsWith("ID_"))
                        {
                            tb.Text = LocalizedStringDatabase.Current.GetString(outcome);
                        }
                        else
                        {
                            tb.Text = outcome;
                        }
                    }
                    // if theres no text then it will just use InstanceName as the text
                    else
                    {
                        tb.Text = uiComponent.Internal.InstanceName;
                    }

                    // basic settings
                    tb.Opacity = element.Opacity;

                    float leftPadding = uiComponent.Internal.AutoAdjustLeftPadding;
                    float rightPadding = uiComponent.Internal.AutoAdjustRightPadding;

                    tb.Padding = new Thickness(leftPadding, 0, rightPadding, 0);

                    if (uiComponent.Internal.Password)
                    {
                        tb.Text = new string('*', tb.Text.Length);
                    }
                    if (uiComponent.Internal.Text.Wordwrap)
                    {
                        tb.TextWrapping = TextWrapping.Wrap;
                    }

                    // setting the actual font
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

                    // sets the alignment of the text
                    switch (uiComponent.Internal.Text.VerticalAlignment.ToString())
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
                            tb.VerticalAlignment = VerticalAlignment.Center;
                            break;
                    }

                    // they spelt horizontal wrong lol
                    switch (uiComponent.Internal.Text.HorizonalAlignment.ToString())
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

                    if (debugging)
                    {
                        App.Logger.Log(uiComponent.Internal.Text.HorizonalAlignment.ToString());
                        App.Logger.Log(uiComponent.Internal.Text.VerticalAlignment.ToString());

                        App.Logger.Log(tb.HorizontalAlignment.ToString());
                        App.Logger.Log(tb.VerticalAlignment.ToString());
                    }

                    // font effect

                    var fontEffectGuid = ((PointerRef)uiComponent.Internal.FontEffect).External.FileGuid;
                    var fontEffectEbx = App.AssetManager.GetEbxEntry(fontEffectGuid);

                    if (fontEffectEbx != null && createFontEffects)
                    {
                        FontEffect fontEffect = new FontEffect();

                        fontEffect.Apply(tb, border, element, fontEffectEbx);
                    }

                    parentCanvas.Children.Add(element);
                    element.Children.Add(border);
                    border.Child = tb;

                    return element;
                }
            }
            else if (componentName == "FrostySdk.Ebx.UIElementFillEntityData" || componentName == "FrostySdk.Ebx.PVZUIElementFillEntityData")
            {
                if (uiComponent.Internal.Visible || showAllUI)
                {
                    UIBlueprintElement element = new UIBlueprintElement(uiComponent, isWidget, Movement);

                    var rect = new System.Windows.Shapes.Rectangle
                    {
                        Width = element.Width,
                        Height = element.Height,
                    };

                    // style
                    var fillGuid = ((PointerRef)uiComponent.Internal.Style).External.FileGuid;
                    var fillEbx = App.AssetManager.GetEbxEntry(fillGuid);

                    EbxAsset fillAsset = App.AssetManager.GetEbx(fillEbx);
                    dynamic rootObjectFill = fillAsset.RootObject;

                    var alpha = (float)rootObjectFill.BackgroundColor.Alpha;

                    var colorR = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.x * 255);
                    var colorG = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.y * 255);
                    var colorB = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.z * 255);

                    rect.Fill = new SolidColorBrush(Color.FromRgb(colorR, colorG, colorB));
                    rect.Opacity = alpha;

                    parentCanvas.Children.Add(element);
                    element.Children.Add(rect);

                    return element;
                }
            }
            else if (componentName == "FrostySdk.Ebx.UIElementButtonEntityData")
            {
                // does nothing for buttons since they are basically just hitboxes

                return null;
            }
            else if (componentName == "FrostySdk.Ebx.UIElementWidgetReferenceEntityData" && createWidgets)
            {
                UIBlueprintElement element = new UIBlueprintElement(uiComponent, isWidget, Movement);

                // gets the reference blueprint of the widget as an EBX
                var widgetGuid = ((PointerRef)uiComponent.Internal.Blueprint).External.FileGuid;
                var widgetEbx = App.AssetManager.GetEbxEntry(widgetGuid);

                EbxAsset widgetAsset = App.AssetManager.GetEbx(widgetEbx);
                dynamic rootObjectWidget = widgetAsset.RootObject;

                var widgetSize = rootObjectWidget.Object.Internal.Size;

                if (!uiComponent.Internal.UseElementSize)
                {
                    element.Width = widgetSize.X;
                    element.Height = widgetSize.Y;
                }
                // if this is true, we'll just use the width/height that is already set in UIBlueprintElement

                double offsetX = uiComponent.Internal.Offset.X;
                double offsetY = uiComponent.Internal.Offset.Y;
                double anchorX = uiComponent.Internal.Anchor.X;
                double anchorY = uiComponent.Internal.Anchor.Y;

                float mainSizeX = rootObject.Object.Internal.Size.X;
                float mainSizeY = rootObject.Object.Internal.Size.Y;

                double widgetFinalX = anchorX * (mainSizeX - widgetSize.X) + offsetX;
                double widgetFinalY = anchorY * (mainSizeY - widgetSize.Y) + offsetY;

                // these colors in widget references are supposed to control the color channels
                // but i dont think there is an easy way to do that with wpf and i dont wanna
                // spend hours just to get widget references to have colors lol

                //byte colorX = (byte)Math.Round(uiComponent.Internal.Color.x * 255);
                //byte colorY = (byte)Math.Round(uiComponent.Internal.Color.y * 255);
                //byte colorZ = (byte)Math.Round(uiComponent.Internal.Color.z * 255);

                element.Opacity = element.Opacity;

                Canvas.SetLeft(element, widgetFinalX);
                Canvas.SetTop(element, widgetFinalY);

                if (debugging)
                {
                    App.Logger.Log("widget");
                }

                parentCanvas.Children.Add(element);

                // repeats everything with the EBX of the widget to render everything that is inside the widget
                LoadUI(widgetEbx, true, element);

                return element;
            }
            else
            {
                // creates a basic rectangle if its an unknown component
                // if you're using this for another game this is what most ui elements will render as

                App.Logger.Log("Unrecognized UI component");

                UIBlueprintElement element = new UIBlueprintElement(uiComponent, isWidget, Movement);

                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = element.Width,
                    Height = element.Height,
                    Fill = Brushes.Orange,
                    Opacity = 0.05,
                };

                var tb = new TextBlock
                {
                    Text = uiComponent.Internal.InstanceName,
                    FontSize = 24,
                    Opacity = 0.2,
                };

                parentCanvas.Children.Add(element);
                element.Children.Add(rect);
                element.Children.Add(tb);

                return element;
            }

            return null;
        }
    }
}
