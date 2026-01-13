using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Controls.Editors;
using Frosty.Core.Screens;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using TexturePlugin;
using static System.Net.Mime.MediaTypeNames;

namespace UIBlueprintEditor
{
    [TemplatePart(Name = PART_SwitchView, Type = typeof(Button))]
    [TemplatePart(Name = PART_DefaultEditorLayer, Type = typeof(Grid))]
    [TemplatePart(Name = PART_UIEditorLayer, Type = typeof(Grid))]
    [TemplatePart(Name = PART_AddObject, Type = typeof(Button))]
    [TemplatePart(Name = PART_UISize, Type = typeof(Grid))]
    [TemplatePart(Name = PART_UICanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_Refresh, Type = typeof(Button))]
    public class UIEditor : FrostyAssetEditor
    {
        private const string PART_SwitchView = "PART_SwitchView";
        private const string PART_DefaultEditorLayer = "PART_DefaultEditorLayer";
        private const string PART_UIEditorLayer = "PART_UIEditorLayer";
        private const string PART_AddObject = "PART_AddObject";
        private const string PART_UISize = "PART_UISize";
        private const string PART_TemplateUI = "PART_TemplateUI";
        private const string PART_UICanvas = "PART_UICanvas";
        private const string PART_Refresh = "PART_Refresh";

        private Button _switchViewButton;
        private FrameworkElement _uiEditorLayer;
        private FrameworkElement _defaultEditorLayer;
        private Button _addObjectButton;
        private FrameworkElement _uiSize;
        private Canvas _uiCanvas;
        private Button _refreshButton;

        private bool _isEditorActive = false;

        public UIEditor(ILogger inLogger) : base(inLogger)
        {
            // App.Logger.Log(App.SelectedAsset.Type.ToString());
        }
        static UIEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UIEditor), new FrameworkPropertyMetadata(typeof(UIEditor)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _uiEditorLayer = GetTemplateChild(PART_UIEditorLayer) as FrameworkElement;
            _defaultEditorLayer = GetTemplateChild(PART_DefaultEditorLayer) as FrameworkElement;

            _switchViewButton = GetTemplateChild(PART_SwitchView) as Button;
            _switchViewButton.Click += SwitchViewButton_Click;

            _addObjectButton = GetTemplateChild(PART_AddObject) as Button;
            _addObjectButton.Click += AddObjectButton_Click;

            _uiSize = GetTemplateChild(PART_UISize) as FrameworkElement;

            _uiCanvas = GetTemplateChild(PART_UICanvas) as Canvas;

            _refreshButton = GetTemplateChild(PART_Refresh) as Button;
            _refreshButton.Click += RefreshButton_Click;
        }

        // switches between the default editor and the ui editor
        private void SwitchViewButton_Click(object sender, RoutedEventArgs e)
        {
            _isEditorActive = !_isEditorActive;
            if (_isEditorActive)
            {
                _uiEditorLayer.Visibility = Visibility.Visible;
                _defaultEditorLayer.Visibility = Visibility.Hidden;

                LoadUI(App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry, false, null);
            }
            else
            {
                _uiEditorLayer.Visibility = Visibility.Hidden;
                _defaultEditorLayer.Visibility = Visibility.Visible;
            }
        }

        private static TextureExporter s_exporter = new TextureExporter();

        bool createImages = false;
        bool createWidgets = false;
        bool createText = false;

        bool debugging = true;

        bool dragging = false;
        float movedX;
        float movedY;

        // loads every asset/component in the ui blueprint that you're currently on
        private void LoadUI(EbxAssetEntry ebxEntry, bool isWidget, Canvas widgetCanvas)
        {
            EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);
            dynamic rootObject = asset.RootObject;

            float mainSizeX = rootObject.Object.Internal.Size.X;
            float mainSizeY = rootObject.Object.Internal.Size.Y;

            _uiCanvas.Width = mainSizeX;
            _uiCanvas.Height = mainSizeY;

            if (debugging)
            {
                App.Logger.Log("");
                App.Logger.Log("---- " + rootObject.Name + " ----");
            }

            if (isWidget == false)
            {
                _uiCanvas.Children.Clear();
                _uiSize.Width = mainSizeX;
                _uiSize.Height = mainSizeY;
            }

            Dictionary<dynamic, dynamic> mappingIdToMapping = new Dictionary<dynamic, dynamic>();
            Dictionary<dynamic, dynamic> mappingMinValue = new Dictionary<dynamic, dynamic>();
            Dictionary<dynamic, dynamic> mappingMaxValue = new Dictionary<dynamic, dynamic>();
            Dictionary<dynamic, BitmapImage> mappingTexture = new Dictionary<dynamic, BitmapImage>();

            foreach (var textureItem in rootObject.Object.Internal.TextureMappings)
            {
                if (debugging)
                {
                    App.Logger.Log("texture");
                }

                var textureMapGuid = ((PointerRef)textureItem).External.FileGuid;
                var textureMapEbx = App.AssetManager.GetEbxEntry(textureMapGuid);

                EbxAsset textureMapAsset = App.AssetManager.GetEbx(textureMapEbx);
                dynamic rootObjectTextureMap = textureMapAsset.RootObject;

                foreach (dynamic outputEntry in rootObjectTextureMap.Output)
                {
                    var min = outputEntry.Min;
                    var max = outputEntry.Max;
                    var textureRef = outputEntry.Texture;

                    var textureGuid = ((PointerRef)textureRef).External.FileGuid;
                    var textureEbx = App.AssetManager.GetEbxEntry(textureGuid);

                    var textureAsset = App.AssetManager.GetEbx(textureEbx);
                    dynamic rootObjectTexture = textureAsset.RootObject;
                    ulong textureRes = ((dynamic)rootObjectTexture).Resource;

                    // texture section by NM (thanks lol)

                    Texture texture = App.AssetManager.GetResAs<Texture>(App.AssetManager.GetResEntry(textureRes));

                    mappingIdToMapping.Add(outputEntry.Id, outputEntry);
                    mappingMinValue.Add(outputEntry.Id, min);
                    mappingMaxValue.Add(outputEntry.Id, max);

                    // Temporary filename.
                    string path = Path.Combine(Environment.CurrentDirectory,
                        string.Format("{0:X16}.png", texture.ResourceId));

                    if (!File.Exists(path))
                    {
                        // `TextureExporter` can't export to a `Stream`, so we'll need to export to the disk first.
                        s_exporter.Export(texture, path, "*.png");
                    }

                    // Read the newly exported image into a `Bitmap`.
                    var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var bitmap = new BitmapImage();

                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();

                    mappingTexture.Add(outputEntry.Id, bitmap);
                }
            }

            foreach (var layer in rootObject.Object.Internal.Layers)
            {
                foreach (var uiComponent in layer.Internal.Elements)
                {
                    if (layer.Internal.Visible == true)
                    {
                        var sizeX = uiComponent.Internal.Size.X;
                        var sizeY = uiComponent.Internal.Size.Y;

                        var offsetX = uiComponent.Internal.Offset.X;
                        var offsetY = uiComponent.Internal.Offset.Y;

                        double anchorX = (double)(uiComponent.Internal.Anchor.X);
                        double anchorY = (double)(uiComponent.Internal.Anchor.Y);

                        double width = (double)(uiComponent.Internal.Size.X);
                        double height = (double)(uiComponent.Internal.Size.Y);
                        double x = (double)(uiComponent.Internal.Offset.X);
                        double y = (double)(uiComponent.Internal.Offset.Y);


                        if (debugging)
                        {
                            App.Logger.Log("{0} Offset: {1} {2}, Size: {3} {4}, Anchor: {5} {6}",
                            uiComponent.Internal.InstanceName,
                            offsetX.ToString(), offsetY.ToString(),
                            sizeX.ToString(), sizeY.ToString(),
                            anchorX.ToString(), anchorY.ToString());
                        }

                        double finalX = anchorX * (mainSizeX - sizeX) + x;
                        double finalY = anchorY * (mainSizeY - sizeY) + y;

                        // objectId by @gabbaton
                        CString objectIdCStr = ((dynamic)uiComponent.Internal).__Id;
                        string objectId = objectIdCStr.ToString();

                        if ((objectId == "UIElementBitmapEntityData" || objectId == "PVZUIElementBitmapEntityData" || objectId == "PVZUIElementDynamicBitmapEntityData" || objectId == "PVZUIElementDynamicBitmapEntityData") && createImages == true)
                        {
                            try
                            {
                                // canvas is used to group each ui component
                                var canvas = new Canvas
                                {
                                    Width = width,
                                    Height = height,
                                    ClipToBounds = true,
                                    VerticalAlignment = VerticalAlignment.Top,
                                    HorizontalAlignment = HorizontalAlignment.Left,
                                };

                                var image = new System.Windows.Controls.Image
                                {
                                    Width = width,
                                    Height = height,
                                    ClipToBounds = true,
                                    VerticalAlignment = VerticalAlignment.Top,
                                    HorizontalAlignment = HorizontalAlignment.Left,
                                };

                                var tb = new TextBlock
                                {
                                    Width = width,
                                    Height = height,
                                    Foreground = System.Windows.Media.Brushes.Lime,
                                    Text = uiComponent.Internal.InstanceName,
                                    VerticalAlignment = VerticalAlignment.Top,
                                    HorizontalAlignment = HorizontalAlignment.Left
                                };

                                string textureMapId = uiComponent.Internal.TextureId;

                                var texture = mappingTexture[textureMapId];

                                double minX = mappingMinValue[textureMapId].x * width;
                                double minY = mappingMinValue[textureMapId].y * height;
                                double maxX = mappingMaxValue[textureMapId].x * width;
                                double maxY = mappingMaxValue[textureMapId].y * height;

                                System.Windows.Point min = new System.Windows.Point(minX, minY);
                                System.Windows.Point max = new System.Windows.Point(maxX, maxY);

                                image.Source = texture;
                                image.Clip = new RectangleGeometry(new Rect(min, max));
                                RenderOptions.SetBitmapScalingMode(image, bitmapScalingMode:BitmapScalingMode.Fant);

                                // scale up to previous size
                                double croppedWidth = maxX - minX;
                                double croppedHeight = maxY - minY;

                                double scaleX = width / croppedWidth;
                                double scaleY = height / croppedHeight;

                                var transformGroup = new TransformGroup();
                                transformGroup.Children.Add(new TranslateTransform(-minX, -minY));
                                transformGroup.Children.Add(new ScaleTransform(scaleX, scaleY));

                                image.RenderTransform = transformGroup;

                                Canvas.SetLeft(canvas, finalX);
                                Canvas.SetTop(canvas, finalY);

                                Canvas.SetLeft(image, finalX);
                                Canvas.SetTop(image, finalY);

                                Canvas.SetLeft(tb, finalX);
                                Canvas.SetTop(tb, finalY);

                                if (uiComponent.Internal.Visible == true)
                                {
                                    if (isWidget)
                                    {
                                        widgetCanvas.Children.Add(canvas);
                                        canvas.Children.Add(image);
                                        //canvas.Children.Add(tb);
                                    }
                                    else
                                    {
                                        _uiCanvas.Children.Add(canvas);
                                        canvas.Children.Add(image);
                                        //canvas.Children.Add(tb);

                                        // comment out if you dont need text on the image
                                    }

                                    _uiCanvas.UpdateLayout();
                                }
                            }
                            catch (Exception ex)
                            {
                                App.Logger.Log("Something went wrong: " + ex);
                                // "An item with the same key" error sometimes happens, idk the exception name so i just did this
                            }
                        }
                        else if ((objectId == "UIElementTextFieldEntityData" || objectId == "PVZUIElementTextFieldEntityData") && createText == true)
                        {
                            var canvas = new Canvas
                            {
                                Width = width,
                                Height = height,
                                ClipToBounds = true,
                                VerticalAlignment = VerticalAlignment.Top,
                                HorizontalAlignment = HorizontalAlignment.Left,
                            };

                            var tb = new TextBlock
                            {
                                Width = width,
                                Height = height,
                                ClipToBounds = true
                            };

                            var colorR = (byte)Math.Round(uiComponent.Internal.Color.x * 255);
                            var colorG = (byte)Math.Round(uiComponent.Internal.Color.y * 255);
                            var colorB = (byte)Math.Round(uiComponent.Internal.Color.z * 255);

                            // font
                            var fontGuid = ((PointerRef)uiComponent.Internal.FontStyle).External.FileGuid;
                            var fontEbx = App.AssetManager.GetEbxEntry(fontGuid);

                            EbxAsset fontAsset = App.AssetManager.GetEbx(fontEbx);
                            dynamic rootObjectFont = fontAsset.RootObject;

                            double fontSize = (double)rootObjectFont.Hd.Internal.PointSize;

                            tb.Text = uiComponent.Internal.Text.Sid;
                            tb.FontSize = fontSize;
                            tb.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(colorR, colorG, colorB));

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
                                    tb.HorizontalAlignment = HorizontalAlignment.Left;
                                    break;
                                case "UIElementAlignment_Center":
                                    tb.HorizontalAlignment = HorizontalAlignment.Center;
                                    break;
                                case "UIElementAlignment_Right":
                                    tb.HorizontalAlignment = HorizontalAlignment.Right;
                                    break;
                                default:
                                    tb.HorizontalAlignment = HorizontalAlignment.Center;
                                    break;
                            }

                            Canvas.SetLeft(canvas, finalX);
                            Canvas.SetTop(canvas, finalY);

                            Canvas.SetLeft(tb, finalX);
                            Canvas.SetTop(tb, finalY);

                            if (uiComponent.Internal.Visible == true)
                            {
                                if (isWidget)
                                {
                                    widgetCanvas.Children.Add(canvas);
                                    canvas.Children.Add(tb);
                                }
                                else
                                {
                                    _uiCanvas.Children.Add(canvas);
                                    canvas.Children.Add(tb);
                                }

                                _uiCanvas.UpdateLayout();
                            }
                        }
                        else if (objectId == "UIElementFillEntityData" || objectId == "PVZUIElementFillEntityData")
                        {
                            var canvas = new Canvas
                            {
                                Width = width,
                                Height = height,
                                ClipToBounds = true,
                                VerticalAlignment = VerticalAlignment.Top,
                                HorizontalAlignment = HorizontalAlignment.Left,
                            };

                            var rect = new System.Windows.Shapes.Rectangle
                            {
                                Width = width,
                                Height = height,
                                VerticalAlignment = VerticalAlignment.Top,
                                HorizontalAlignment = HorizontalAlignment.Left
                            };

                            // style
                            var fillGuid = ((PointerRef)uiComponent.Internal.Style).External.FileGuid;
                            var fillEbx = App.AssetManager.GetEbxEntry(fillGuid);

                            EbxAsset fillAsset = App.AssetManager.GetEbx(fillEbx);
                            dynamic rootObjectFill = fillAsset.RootObject;

                            var colorR = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.x * 255);
                            var colorG = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.y * 255);
                            var colorB = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.z * 255);

                            rect.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(colorR, colorG, colorB));

                            Canvas.SetLeft(canvas, finalX);
                            Canvas.SetTop(canvas, finalY);

                            Canvas.SetLeft(rect, finalX);
                            Canvas.SetTop(rect, finalY);

                            if (uiComponent.Internal.Visible == true)
                            {
                                if (isWidget)
                                {
                                    widgetCanvas.Children.Add(canvas);
                                    canvas.Children.Add(rect);
                                }
                                else
                                {
                                    _uiCanvas.Children.Add(canvas);
                                    canvas.Children.Add(rect);
                                }

                                _uiCanvas.UpdateLayout();
                            }
                        }
                        else if (objectId == "UIElementButtonEntityData")
                        {
                            // does nothing for buttons since they are basically just hitboxes
                        }
                        else if ((objectId == "UIElementWidgetReferenceEntityData") && createWidgets == true)
                        {
                            var viewBox = new Viewbox
                            {
                                Width = width,
                                Height = height,
                                VerticalAlignment = VerticalAlignment.Top,
                                HorizontalAlignment = HorizontalAlignment.Left,
                            };

                            var canvas = new Canvas
                            {
                                Width = width,
                                Height = height,
                                VerticalAlignment = VerticalAlignment.Top,
                                HorizontalAlignment = HorizontalAlignment.Left,
                            };

                            var widgetGuid = ((PointerRef)uiComponent.Internal.Blueprint).External.FileGuid;
                            var widgetEbx = App.AssetManager.GetEbxEntry(widgetGuid);

                            if (debugging)
                            {
                                App.Logger.Log("widget");
                            }

                            Canvas.SetLeft(viewBox, finalX);
                            Canvas.SetTop(viewBox, finalY);

                            Canvas.SetLeft(canvas, finalX);
                            Canvas.SetTop(canvas, finalY);

                            if (isWidget)
                            {
                                widgetCanvas.Children.Add(viewBox);
                                viewBox.Child = canvas;
                            }
                            else
                            {
                                _uiCanvas.Children.Add(viewBox);
                                viewBox.Child = canvas;
                            }

                            _uiCanvas.UpdateLayout();

                            LoadUI(widgetEbx, true, canvas);
                        }
                        else
                        {
                            // create a basic rectangle if its an unkown component

                            App.Logger.Log("Unrecongnized UI component");
                            var canvas = new Canvas
                            {
                                Width = width,
                                Height = height,
                                ClipToBounds = true,
                                VerticalAlignment = VerticalAlignment.Top,
                                HorizontalAlignment = HorizontalAlignment.Left,
                            };

                            var rect = new System.Windows.Shapes.Rectangle
                            {
                                Width = width,
                                Height = height,
                                Fill = System.Windows.Media.Brushes.Orange,
                                Opacity = 0.05,
                                ClipToBounds = true,
                                VerticalAlignment = VerticalAlignment.Top,
                                HorizontalAlignment = HorizontalAlignment.Left
                            };

                            var tb = new TextBlock
                            {
                                Text = uiComponent.Internal.InstanceName,
                                FontSize = 24,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Opacity = 0.2
                            };

                            Canvas.SetLeft(canvas, finalX);
                            Canvas.SetTop(canvas, finalY);

                            Canvas.SetLeft(rect, finalX);
                            Canvas.SetTop(rect, finalY);

                            Canvas.SetLeft(tb, finalX);
                            Canvas.SetTop(tb, finalY);

                            if (isWidget)
                            {
                                widgetCanvas.Children.Add(canvas);
                                canvas.Children.Add(rect);
                                canvas.Children.Add(tb);
                            }
                            else
                            {
                                _uiCanvas.Children.Add(canvas);
                                canvas.Children.Add(rect);
                                canvas.Children.Add(tb);
                                
                                ControlUI(canvas);

                                if (dragging)
                                {
                                    dynamic component = uiComponent.Internal.Resolve();

                                    component.Offset.X = movedX;
                                    component.Offset.Y = movedY;

                                    App.AssetManager.ModifyEbx(rootObject.Name, asset);
                                }
                            }

                            _uiCanvas.UpdateLayout();
                        }
                    }
                }
            }
        }

        System.Windows.Point startPosition;
        private void ControlUI(Canvas canvas)
        {
            canvas.MouseDown += CanvasMouseDown;
            canvas.MouseMove += CanvasMouseMove;
            canvas.MouseUp += CanvasMouseUp;
        }

        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            Canvas canvas = sender as Canvas;

            dragging = true;
            startPosition = Mouse.GetPosition(_uiCanvas);
        }

        private void CanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            dragging = false;
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Canvas canvas = sender as Canvas;

                System.Windows.Point newPosition = Mouse.GetPosition(_uiCanvas);

                double left = Canvas.GetLeft(canvas);
                double top = Canvas.GetTop(canvas);

                Canvas.SetLeft(canvas, Math.Round(left + (newPosition.X - startPosition.X)));
                Canvas.SetTop(canvas, Math.Round(top + (newPosition.Y - startPosition.Y)));
                startPosition = newPosition;

                movedX = (float)Math.Round(left + (newPosition.X - startPosition.X));
                movedY = (float)Math.Round(left + (newPosition.Y - startPosition.Y));

                App.Logger.Log(Canvas.GetLeft(canvas).ToString());
                App.Logger.Log(Canvas.GetTop(canvas).ToString());
            }
        }

        // refreshes the layout in case any ui values change
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _uiCanvas.UpdateLayout();
        }

        // unused thing
        private void AddObjectButton_Click(object sender, RoutedEventArgs e)
        {
            App.Logger.Log("added object");
        }
    }
}
