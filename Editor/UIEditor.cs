using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using Frosty.Hash;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UIBlueprintEditor.Editor.Misc;
using UIBlueprintEditor.Editor.UI;

namespace UIBlueprintEditor.Editor
{
    // the more i update this plugin the more ui parts it needs lol
    // let me know if theres a better way of doing this, it looks really ugly
    #region Template Parts
    [TemplatePart(Name = PART_SwitchView, Type = typeof(Button))]
    [TemplatePart(Name = PART_DefaultEditorLayer, Type = typeof(Grid))]
    [TemplatePart(Name = PART_UIEditorLayer, Type = typeof(Grid))]
    [TemplatePart(Name = PART_UISize, Type = typeof(Grid))]
    [TemplatePart(Name = PART_UICanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_Refresh, Type = typeof(Button))]
    [TemplatePart(Name = PART_Precise, Type = typeof(Button))]
    [TemplatePart(Name = PART_PreciseImage, Type = typeof(Button))]
    [TemplatePart(Name = PART_UIElementInfo, Type = typeof(TextBlock))]
    [TemplatePart(Name = PART_Unhide, Type = typeof(Button))]
    [TemplatePart(Name = PART_UISizeText, Type = typeof(TextBlock))]
    [TemplatePart(Name = PART_ZoomPercent, Type = typeof(TextBlock))]
    [TemplatePart(Name = PART_AssetPropertyGrid, Type = typeof(FrostyPropertyGrid))]
    [TemplatePart(Name = PART_BackgroundGrid, Type = typeof(FrostyPropertyGrid))]
    [TemplatePart(Name = PART_Column1, Type = typeof(ColumnDefinition))]
    [TemplatePart(Name = PART_ColumnSplitter, Type = typeof(ColumnDefinition))]
    [TemplatePart(Name = PART_Column2, Type = typeof(ColumnDefinition))]
    [TemplatePart(Name = PART_TabToolbox, Type = typeof(FrostyTabItem))]
    [TemplatePart(Name = PART_TabProperties, Type = typeof(FrostyTabItem))]
    [TemplatePart(Name = PART_TabPropertiesContent, Type = typeof(FrostyPropertyGrid))]
    [TemplatePart(Name = PART_TabControl, Type = typeof(FrostyTabControl))]
    [TemplatePart(Name = PART_TabToolboxContent, Type = typeof(StackPanel))]
    #endregion
    public class UIEditor : FrostyAssetEditor
    {
        #region UI Parts
        private const string PART_SwitchView = "PART_SwitchView";
        private const string PART_DefaultEditorLayer = "PART_DefaultEditorLayer";
        private const string PART_UIEditorLayer = "PART_UIEditorLayer";
        private const string PART_UISize = "PART_UISize";
        private const string PART_TemplateUI = "PART_TemplateUI";
        private const string PART_UICanvas = "PART_UICanvas";
        private const string PART_Refresh = "PART_Refresh";
        private const string PART_Precise = "PART_Precise";
        private const string PART_PreciseImage = "PART_PreciseImage";
        private const string PART_UIElementInfo = "PART_UIElementInfo";
        private const string PART_Unhide = "PART_Unhide";
        private const string PART_UISizeText = "PART_UISizeText";
        private const string PART_ZoomPercent = "PART_ZoomPercent";
        private const string PART_AssetPropertyGrid = "PART_AssetPropertyGrid";
        private const string PART_BackgroundGrid = "PART_BackgroundGrid";
        private const string PART_Column1 = "PART_Column1";
        private const string PART_ColumnSplitter = "PART_ColumnSplitter";
        private const string PART_Column2 = "PART_Column2";
        private const string PART_TabToolbox = "PART_TabToolbox";
        private const string PART_TabProperties = "PART_TabProperties";
        private const string PART_TabPropertiesContent = "PART_TabPropertiesContent";
        private const string PART_TabControl = "PART_TabControl";
        private const string PART_TabToolboxContent = "PART_TabToolboxContent";

        private Button _switchViewButton;
        private FrameworkElement _uiEditorLayer;
        private FrameworkElement _defaultEditorLayer;
        private FrameworkElement _uiSize;
        private TextBlock _zoomPercent;
        private Image _preciseImage;
        private ImageBrush _backgroundGrid;
        private FrostyPropertyGrid _pgAsset;
        private ColumnDefinition _column1;
        private ColumnDefinition _columnSplitter;
        private ColumnDefinition _column2;
        private FrostyTabItem _tabToolbox;
        private FrostyTabControl _tabControl;
        private Canvas _uiCanvas;
        private Button _refreshButton;
        private Button _preciseButton;
        private Button _unhideButton;
        private TextBlock _uiSizeText;
        private TextBlock _uiElementInfo;
        private FrostyTabItem _tabProperties;
        private FrostyPropertyGrid _tabPropertiesContent;
        private StackPanel _tabToolboxContent;
        #endregion

        public static Dictionary<dynamic, dynamic> MappingIdToMapping = new Dictionary<dynamic, dynamic>();
        public static Dictionary<dynamic, dynamic> MappingMinValue = new Dictionary<dynamic, dynamic>();
        public static Dictionary<dynamic, dynamic> MappingMaxValue = new Dictionary<dynamic, dynamic>();
        public static Dictionary<dynamic, BitmapImage> MappingTexture = new Dictionary<dynamic, BitmapImage>();

        public static int RoundTo = 1;

        private Movement _movement;
        private TransformGroup _transformGroup;
        private ScaleTransform _scaleTransform;
        private TranslateTransform _translateTransform;
        private bool _editorActive = false;
        private bool _panning = false;

		private EbxAssetEntry _ebxEntry;
		private EbxAsset _ebxAsset;
		private dynamic _rootObject;

		public UIEditor(ILogger inLogger) : base(inLogger)
        {
            MouseWheel += UICanvas_MouseWheel;
            MouseDown += UICanvas_MouseDown;
            MouseUp += UICanvas_MouseUp;
            MouseMove += UICanvas_MouseMove;

            _transformGroup = new TransformGroup();
            _scaleTransform = new ScaleTransform(1, 1);
            _translateTransform = new TranslateTransform();

            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);
        }

        static UIEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UIEditor), new FrameworkPropertyMetadata(typeof(UIEditor)));
        }

        #region OnApplyTemplate
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _uiEditorLayer = GetTemplateChild(PART_UIEditorLayer) as FrameworkElement;

            _defaultEditorLayer = GetTemplateChild(PART_DefaultEditorLayer) as FrameworkElement;

            _switchViewButton = GetTemplateChild(PART_SwitchView) as Button;
            _switchViewButton.Click += SwitchViewButton_Click;

            _uiSize = GetTemplateChild(PART_UISize) as FrameworkElement;

            _uiCanvas = GetTemplateChild(PART_UICanvas) as Canvas;

            _refreshButton = GetTemplateChild(PART_Refresh) as Button;
            _refreshButton.Click += RefreshButton_Click;

            _preciseButton = GetTemplateChild(PART_Precise) as Button;
            _preciseButton.Click += PreciseButton_Click;

            _preciseImage = GetTemplateChild(PART_PreciseImage) as Image;

            _uiElementInfo = GetTemplateChild(PART_UIElementInfo) as TextBlock;

            _unhideButton = GetTemplateChild(PART_Unhide) as Button;
            _unhideButton.Click += UnhideButton_Click;

            _uiSizeText = GetTemplateChild(PART_UISizeText) as TextBlock;

            _zoomPercent = GetTemplateChild(PART_ZoomPercent) as TextBlock;

            _backgroundGrid = GetTemplateChild(PART_BackgroundGrid) as ImageBrush;

            _pgAsset = GetTemplateChild(PART_AssetPropertyGrid) as FrostyPropertyGrid;

            _column1 = GetTemplateChild(PART_Column1) as ColumnDefinition;
            _columnSplitter = GetTemplateChild(PART_ColumnSplitter) as ColumnDefinition;
            _column2 = GetTemplateChild(PART_Column2) as ColumnDefinition;

            _tabToolbox = GetTemplateChild(PART_TabToolbox) as FrostyTabItem;
            _tabProperties = GetTemplateChild(PART_TabProperties) as FrostyTabItem;

            _tabPropertiesContent = GetTemplateChild(PART_TabPropertiesContent) as FrostyPropertyGrid;
            _tabToolboxContent = GetTemplateChild(PART_TabToolboxContent) as StackPanel;

            _tabControl = GetTemplateChild(PART_TabControl) as FrostyTabControl;

            foreach (Button button in _tabToolboxContent.Children)
            {
                button.Click += (sender, e) => { ToolboxButton_Click(sender, e, _tabToolboxContent.Children.IndexOf(button)); };
            }

            _movement = new Movement(LoadUI, _uiCanvas, _refreshButton, _preciseButton, _unhideButton, _uiSizeText, _uiElementInfo, _tabProperties, _tabPropertiesContent);
            KeyDown += _movement.UICanvasKeyDown;
            KeyUp += _movement.UICanvasKeyUp;
            MouseDown += (sender, e) =>
            {
                _uiCanvas.Focus();
            };

			UpdateRootObject();
		}
        #endregion

        private void SwitchViewButton_Click(object sender, RoutedEventArgs e)
        {
			_editorActive = !_editorActive;
            if (_editorActive)
            {
				_uiEditorLayer.Visibility = Visibility.Visible;
                _defaultEditorLayer.Visibility = Visibility.Hidden;

                _column1.Width = new GridLength(0.8, GridUnitType.Star);
                _columnSplitter.Width = new GridLength(3);
                _column2.Width = new GridLength(0.2, GridUnitType.Star);

                _tabControl.SelectedIndex = 0;

                UpdateRootObject();
                LoadUI(_rootObject, false, null);
			}
            else
            {
                _uiEditorLayer.Visibility = Visibility.Hidden;
                _defaultEditorLayer.Visibility = Visibility.Visible;

                _column1.Width = new GridLength(1, GridUnitType.Star);
                _columnSplitter.Width = new GridLength(0);
                _column2.Width = new GridLength(0, GridUnitType.Star);

                // refreshes the property grid
                _pgAsset.Object = null;
				_pgAsset.Object = _rootObject;

				MappingIdToMapping.Clear();
                MappingMinValue.Clear();
                MappingMaxValue.Clear();
                MappingTexture.Clear();

                _movement.SelectedElement = null;
                _movement.SelectedCanvas = null;
            }
		}

        private void LoadUI(dynamic rootObject, bool isWidget, Canvas widgetCanvas)
        {
            float mainSizeX = rootObject.Object.Internal.Size.X;
            float mainSizeY = rootObject.Object.Internal.Size.Y;

            if (!isWidget)
            {
                // some stuff that should only run once

                _uiCanvas.Children.Clear();
                _uiSize.Width = mainSizeX;
                _uiSize.Height = mainSizeY;

                _uiSizeText.Text = $"Size: {mainSizeX}, {mainSizeY}";

                _uiElementInfo.Text = "InstanceName: ''\nOffset: 0, 0\nAnchor: 0, 0\n00000000-0000-0000-0000-000000000000";
                _tabProperties.IsEnabled = false;
            }

            bool showAllUI = Config.Get("ShowAllUI", false);

            Canvas parentCanvas = widgetCanvas ?? _uiCanvas;

            foreach (var layer in rootObject.Object.Internal.Layers)
            {
                foreach (var uiElement in layer.Internal.Elements)
                {
                    if (layer.Internal.Visible || showAllUI)
                    {
                        ElementLoader.LoadElement(uiElement, isWidget, _movement, rootObject, parentCanvas, (Action<dynamic, bool, Canvas>)LoadUI);
                    }
                }
            }

            if (rootObject.Object.Internal.ToString() == "FrostySdk.Ebx.UIListWidgetData")
            {
                ElementLoader.LoadList(rootObject, parentCanvas, _movement, (Action<dynamic, bool, Canvas>)LoadUI);
            }

            _uiCanvas.UpdateLayout();
        }

        #region Toolbox Tab
        private void ToolboxButton_Click(object sender, RoutedEventArgs e, int index)
        {
            switch (index)
            {
                case 0:
                    AddItem("PVZUIElementBitmapEntityData");
                    break;
                case 1:
                    AddItem("PVZUIElementTextFieldEntityData");
                    break;
                case 2:
                    AddItem("PVZUIElementFillEntityData");
                    break;
                case 3:
                    AddItem("UIElementWidgetReferenceEntityData");
                    break;
                case 4:
                    AddItem("other");
                    break;
            }
        }

        private void AddItem(string item)
        {
			List<PointerRef> layers = _rootObject.Object.Internal.Layers;

			dynamic layer = null;
			if (layers.Count > 0)
			{
				layer = layers.Last();
			}

			if (layer == null || layer.Internal.LayerName != "UIBlueprintEditor Layer")
			{
				dynamic layerObj = TypeLibrary.CreateObject("UIElementLayerEntityData");

				layerObj.LayerName = "UIBlueprintEditor Layer";
				layerObj.Visible = true;
				layerObj.InclusionSettings.IsSingleplayerLayer = true;
				layerObj.InclusionSettings.IsMultiplayerLayer = true;
				layerObj.InclusionSettings.IsSDLayer = true;
				layerObj.InclusionSettings.IsHDLayer = true;

				layerObj.SetInstanceGuid(new AssetClassGuid(Guid.NewGuid(), -1));

				_ebxAsset.AddObject(layerObj);
				PointerRef layerRef = new PointerRef(internalRef: layerObj);

				layer = layerRef;

				layers.Add(layerRef);
			}

			if (item == "other")
            {
                List<Type> types = new List<Type>();
                foreach (Type type in TypeLibrary.GetTypes("GameDataContainer"))
                {
                    types.Add(type);
                }

                ClassSelector classSelector = new ClassSelector(types.ToArray());
                if (classSelector.ShowDialog() == true)
                {
                    if (classSelector.SelectedClass != null)
                    {
                        item = classSelector.SelectedClass.Name;
                    }
                }
            }

            dynamic element = TypeLibrary.CreateObject(item);
			_ebxAsset.AddObject(element);
            PointerRef elementRef = new PointerRef(internalRef: element);

            #region Default Settings

            try
            {
                ApplyBasicSettings(element);
            }
            catch
            {
                App.Logger.LogError("Must be a UI element");
				_ebxAsset.RemoveObject(element);
                return;
            }

            switch (item)
            {
                case "PVZUIElementBitmapEntityData":
					element.UVRect.z = (float)1;
                    element.UVRect.w = (float)1;
                    element.DistanceFieldParams.AlphaThreshold = (float)0.496;
                    element.DistanceFieldParams.DistanceScale = (float)5;
                    element.DistanceFieldParams.OutlineColor.Rgb.x = (float)1;
                    element.DistanceFieldParams.OutlineColor.Rgb.y = (float)1;
                    element.DistanceFieldParams.OutlineColor.Rgb.z = (float)1;
                    element.DistanceFieldParams.OutlineColor.Alpha = (float)1;
                    element.BlendMode = Enum.Parse(element.BlendMode.GetType(), "UIBlendMode_AlphaBlend");
                    break;
                case "PVZUIElementTextFieldEntityData":
					element.Text.Sid = "Lorem Ipsum";
                    element.Text.Wordwrap = true;
                    element.FontStyle = CreateRef("UI/Font/Styles/Cafeteria36pt");
                    element.AutoAdjustLeftPadding = (float)5;
                    element.AutoAdjustRightPadding = (float)5;
                    element.TextScale = (float)1;
                    element.VerticalAlignOverride = -1;
                    element.HorizontalAlignOverride = -1;
                    break;
                case "PVZUIElementFillEntityData":
					element.BackgroundBlendMode = Enum.Parse(element.BackgroundBlendMode.GetType(), "UIBlendMode_AlphaBlend");
                    element.OutlineBlendMode = Enum.Parse(element.OutlineBlendMode.GetType(), "UIBlendMode_AlphaBlend");
                    element.DrawBackground = true;
                    element.DrawOutline = true;
                    element.Style = CreateRef("UI/Style/HUD/GenericFillWhite");
                    break;
                case "UIElementWidgetReferenceEntityData":
					element.CastSunShadowEnable = true;
                    element.CastReflectionEnable = true;
                    element.CastEnvmapEnable = true;
                    element.LocalPlayerId = Enum.Parse(element.LocalPlayerId.GetType(), "LocalPlayerId_Invalid");
                    element.InclusionSettings.IsSingleplayerLayer = true;
                    element.InclusionSettings.IsMultiplayerLayer = true;
                    element.InclusionSettings.IsSDLayer = true;
                    element.InclusionSettings.IsHDLayer = true;
                    break;
            }
            #endregion

            layer.Internal.Elements.Add(elementRef);

            App.AssetManager.ModifyEbx(_ebxEntry.Name, _ebxAsset);
            App.EditorWindow.DataExplorer.RefreshItems();

			ElementLoader.LoadElement(elementRef, false, _movement, _rootObject, _uiCanvas, (Action<dynamic, bool, Canvas>)LoadUI);
		}

        private void ApplyBasicSettings(dynamic element)
        {
			element.InstanceName = "New Element";
			element.InstanceNameHash = (uint)Fnv1.HashString(element.InstanceName.ToString().ToLower());
			element.UIElementTransform.RotationPivot.x = (float)0.5;
            element.UIElementTransform.RotationPivot.y = (float)0.5;
            element.Size.X = (float)256;
            element.Size.Y = (float)256;
            element.Anchor.X = (float)0.5;
            element.Anchor.Y = (float)0.5;
            element.Color.x = (float)1;
            element.Color.y = (float)1;
            element.Color.z = (float)1;
            element.Alpha = (float)1;
            try { element.Visible = true; } catch { } // widget references dont have the visible property

            element.SetInstanceGuid(new AssetClassGuid(Guid.NewGuid(), -1));
        }

        private PointerRef CreateRef(string assetPath)
        {
            EbxAssetEntry entry = App.AssetManager.GetEbxEntry(assetPath);
            EbxAsset refAsset = App.AssetManager.GetEbx(entry);

            _ebxAsset.AddDependency(entry.Guid);

            return new PointerRef(new EbxImportReference()
            {
                FileGuid = entry.Guid,
                ClassGuid = refAsset.RootInstanceGuid
            });
        }
        #endregion

        #region Zooming
        private void UICanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            bool zoomOut = e.Delta < 0;

            double maxZoom = 3;
            double minZoom = 0.2;

            double zoomValue = 0.1;

            _uiSize.RenderTransformOrigin = new Point(0.5, 0.5);

            _uiSize.RenderTransform = _transformGroup;

            double scale = _scaleTransform.ScaleX;

            if (zoomOut)
            {
                scale -= zoomValue;
            }
            else
            {
                scale += zoomValue;
            }

            scale = Clamp(scale, minZoom, maxZoom);

            _scaleTransform.ScaleX = scale;
            _scaleTransform.ScaleY = scale;

            _backgroundGrid.Viewport = new Rect(0, 0, scale * 28, scale * 28);

            _zoomPercent.Text = Math.Round(scale * 100) + "% Zoom";
        }

        // Math.Clamp is annoyingly missing so its added here
        private double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }
            else if (value > max)
            {
                return max;
            }

            return value;
        }
        #endregion

        #region Panning

        private Point startPositionPan;

        private Point lastPosition;

        private void UICanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _panning = true;

                startPositionPan = Mouse.GetPosition(this);
                lastPosition = new Point(_translateTransform.X, _translateTransform.Y);
            }
        }

        private void UICanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _panning = false;
            }
        }

        private void UICanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_panning)
            {
                Point mousePosition = e.GetPosition(this);
                Point newPosition = new Point(mousePosition.X, mousePosition.Y);

                double averageSize = (_uiSize.ActualWidth + _uiSize.ActualHeight) / 2;
                double panMultiplier = averageSize / 850;

                _uiSize.RenderTransformOrigin = new Point(0.5, 0.5);

                _uiSize.RenderTransform = _transformGroup;

                _translateTransform.X = lastPosition.X + (newPosition.X - startPositionPan.X) * panMultiplier;
                _translateTransform.Y = lastPosition.Y + (newPosition.Y - startPositionPan.Y) * panMultiplier;

                TranslateTransform gridTransform = new TranslateTransform();
                gridTransform.X = lastPosition.X + (newPosition.X - startPositionPan.X);
                gridTransform.Y = lastPosition.Y + (newPosition.Y - startPositionPan.Y);

                _backgroundGrid.Transform = new MatrixTransform(gridTransform.Value);
            }
        }
		#endregion

		private void UpdateRootObject()
		{
			_ebxEntry = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;
			_ebxAsset = asset;
			_rootObject = _ebxAsset.RootObject;
		}

		public void UnhideButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (Canvas canvas in _uiCanvas.Children)
            {
                canvas.Visibility = Visibility.Visible;
            }
        }

        private bool isPrecise = true;

        public void PreciseButton_Click(object sender, RoutedEventArgs e)
        {
            isPrecise = !isPrecise;

            ImageSource offIcon = new ImageSourceConverter().ConvertFromString("pack://application:,,,/UIBlueprintEditor;component/Images/Precise_OFF.png") as ImageSource;
            ImageSource onIcon = new ImageSourceConverter().ConvertFromString("pack://application:,,,/UIBlueprintEditor;component/Images/Precise_ON.png") as ImageSource;

            if (isPrecise)
            {
                RoundTo = 1;

                App.Logger.Log("Turned Precise Movement on");

                _preciseButton.ToolTip = "Precise Movement (ON)";
                _preciseImage.Source = onIcon;
            }
            else
            {
                RoundTo = Config.Get("PreciseMovementSetting", 25);

                App.Logger.Log("Turned Precise Movement off");

                _preciseButton.ToolTip = "Precise Movement (OFF)";
                _preciseImage.Source = offIcon;
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            MappingIdToMapping.Clear();
            MappingMinValue.Clear();
            MappingMaxValue.Clear();
            MappingTexture.Clear();

            _uiCanvas.Children.Clear();

            LoadUI(_rootObject, false, null);

            _uiCanvas.UpdateLayout();

            _tabControl.SelectedIndex = 0;

			UpdateRootObject();

			App.Logger.Log("Refreshed UI");
        }
    }
}
