using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    [TemplatePart(Name = PART_UIComponentInfo, Type = typeof(TextBlock))]
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
        private const string PART_UIComponentInfo = "PART_UIComponentInfo";
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
        private FrostyTabItem _tabLayers;
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
        private StackPanel _tabLayersContent;
        private Button _tabLayersUp;
        private Button _tabLayersDown;
        #endregion

        public static readonly bool Debugging = false; // make this 'true' to have a lot of useful info logged

        // these dictionaries are used later to reference certain values using the TextureId as the key
        public static Dictionary<dynamic, dynamic> MappingIdToMapping = new Dictionary<dynamic, dynamic>();
        public static Dictionary<dynamic, dynamic> MappingMinValue = new Dictionary<dynamic, dynamic>();
        public static Dictionary<dynamic, dynamic> MappingMaxValue = new Dictionary<dynamic, dynamic>();
        public static Dictionary<dynamic, BitmapImage> MappingTexture = new Dictionary<dynamic, BitmapImage>();

        // this is used for the precise movement / snapping
        public static int RoundTo = 1;

        private Movement _movement;
        private Canvas _selectedLayer;
        private TransformGroup _transformGroup;
        private ScaleTransform _scaleTransform;
        private TranslateTransform _translateTransform;
        private Action<object> _refreshPropertyGrid;
        private bool _editorActive = false;
        private bool _panning = false;

        public UIEditor(ILogger inLogger) : base(inLogger)
        {
            // pan/zoom stuff
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

            _uiElementInfo = GetTemplateChild(PART_UIComponentInfo) as TextBlock;

            _unhideButton = GetTemplateChild(PART_Unhide) as Button;
            _unhideButton.Click += UnhideButton_Click;

            _uiSizeText = GetTemplateChild(PART_UISizeText) as TextBlock;

            _zoomPercent = GetTemplateChild(PART_ZoomPercent) as TextBlock;

            _backgroundGrid = GetTemplateChild(PART_BackgroundGrid) as ImageBrush;

            _pgAsset = GetTemplateChild(PART_AssetPropertyGrid) as FrostyPropertyGrid;
            _refreshPropertyGrid = new Action<object>((_) =>
            {
                _pgAsset.Object = null;
                _pgAsset.Object = asset.RootObject;
            });

            _column1 = GetTemplateChild(PART_Column1) as ColumnDefinition;
            _columnSplitter = GetTemplateChild(PART_ColumnSplitter) as ColumnDefinition;
            _column2 = GetTemplateChild(PART_Column2) as ColumnDefinition;

            _tabToolbox = GetTemplateChild(PART_TabToolbox) as FrostyTabItem;
            _tabProperties = GetTemplateChild(PART_TabProperties) as FrostyTabItem;

            _tabPropertiesContent = GetTemplateChild(PART_TabPropertiesContent) as FrostyPropertyGrid;

            _tabControl = GetTemplateChild(PART_TabControl) as FrostyTabControl;

            // arrow key/WASD precise movement
            _movement = new Movement(LoadUI, _uiCanvas, _refreshButton, _preciseButton, _unhideButton, _uiSizeText, _uiElementInfo, _tabProperties, _tabPropertiesContent);
            KeyDown += _movement.UICanvasKeyDown;
            KeyUp += _movement.UICanvasKeyUp;
        }
        #endregion

        // switches between the default editor and the ui editor
        private void SwitchViewButton_Click(object sender, RoutedEventArgs e)
        {
            // toggles the bool
            _editorActive = !_editorActive;
            if (_editorActive)
            {
                // hides the default view and shows the editor view 
                _uiEditorLayer.Visibility = Visibility.Visible;
                _defaultEditorLayer.Visibility = Visibility.Hidden;

                // sets the columns width to fit the tabs on the right
                _column1.Width = new GridLength(0.8, GridUnitType.Star);
                _columnSplitter.Width = new GridLength(3);
                _column2.Width = new GridLength(0.2, GridUnitType.Star);

                _tabControl.SelectedIndex = 0;

                // gets the opened asset as an EbxAssetEntry

                EbxAssetEntry openedAsset = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;

                if (openedAsset == null)
                    return;

                // loads all the ui elements with the openedAsset, isWidget as false
                // and no widget canvas since we aren't loading a widget
                LoadUI(openedAsset, false, null);
            }
            else
            {
                // hides the editor view and shows the default view
                _uiEditorLayer.Visibility = Visibility.Hidden;
                _defaultEditorLayer.Visibility = Visibility.Visible;

                _column1.Width = new GridLength(1, GridUnitType.Star);
                _columnSplitter.Width = new GridLength(0);
                _column2.Width = new GridLength(0, GridUnitType.Star);

                // refreshes the asset that you're on so any changes you made won't be overwritten if you
                // change something in the normal editor
                _refreshPropertyGrid.Invoke(null);

                // clears the texture dictionaries so that new textures will be created everytime
                MappingIdToMapping.Clear();
                MappingMinValue.Clear();
                MappingMaxValue.Clear();
                MappingTexture.Clear();

                _tabLayersContent.Children.Clear();

                _movement.SelectedElement = null;
                _movement.SelectedCanvas = null;

                _selectedLayer = null;
                _tabLayersUp.IsEnabled = false;
                _tabLayersDown.IsEnabled = false;
            }
        }

        // loads every asset/component in the ui blueprint that you're currently on
        private void LoadUI(EbxAssetEntry ebxEntry, bool isWidget, Canvas widgetCanvas)
        {
            EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);
            dynamic rootObject = asset.RootObject;

            if (Debugging)
            {
                App.Logger.Log("");
                App.Logger.Log("---- " + rootObject.Name + " ----");
            }

            float mainSizeX = rootObject.Object.Internal.Size.X;
            float mainSizeY = rootObject.Object.Internal.Size.Y;

            if (!isWidget)
            {
                // some stuff that should only run once

                _uiCanvas.Children.Clear();
                _uiSize.Width = mainSizeX;
                _uiSize.Height = mainSizeY;

                _uiSizeText.Text = string.Format("Size: {0}, {1}", mainSizeX, mainSizeY);

                _uiElementInfo.Text = "InstanceName: ''\nOffset: 0, 0\nAnchor: 0, 0\n00000000-0000-0000-0000-000000000000";
                _tabProperties.IsEnabled = false;
            }

            bool ShowAllUI = Config.Get("ShowAllUI", false);

            // loops through the "Layers"
            foreach (var layer in rootObject.Object.Internal.Layers)
            {
                // loops through each component in each layer
                foreach (var uiElement in layer.Internal.Elements)
                {
                    // the ui will only render if the Visible property of the layer is true
                    if (layer.Internal.Visible || ShowAllUI)
                    {
                        Canvas parentCanvas = widgetCanvas ?? _uiCanvas;

                        LoadElement.Load(uiElement, isWidget, _movement, rootObject, parentCanvas, (Action<EbxAssetEntry, bool, Canvas>)LoadUI);
                    }
                }
            }

            // update layout once everything is loaded
            _uiCanvas.UpdateLayout();
        }

        #region Zooming
        private void UICanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // if e.Delta is less than 0, we're zooming out
            bool zoomOut = e.Delta < 0;

            double maxZoom = 3;
            double minZoom = 0.2;

            // this value is how much it will be zoomed by
            double zoomValue = 0.1;

            // this is the center of '_uiSize' and not the center of the screen
            // so if you pan out it wont zoom from the center which is annoying and idk how i would fix it
            _uiSize.RenderTransformOrigin = new Point(0.5, 0.5);

            _uiSize.RenderTransform = _transformGroup;

            double scale = _scaleTransform.ScaleX;

            if (zoomOut)
            {
                // zoom out
                scale -= zoomValue;
            }
            else
            {
                // zoom in
                scale += zoomValue;
            }

            scale = Clamp(scale, minZoom, maxZoom);

            _scaleTransform.ScaleX = scale;
            _scaleTransform.ScaleY = scale;

            // sets the background grid so that it looks like the background is also zooming in/out
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

                double panMultiplier;

                // this will make it so the pan multiplier will scale with the size of the ui blueprint
                // otherwise on small ui blueprints the panning would be too fast or too slow on some UIs

                double averageSize = (_uiSize.ActualWidth + _uiSize.ActualHeight) / 2;
                panMultiplier = averageSize / 850; // the '850' is basically the overall speed

                // pans from the center
                _uiSize.RenderTransformOrigin = new Point(0.5, 0.5);

                _uiSize.RenderTransform = _transformGroup;

                _translateTransform.X = lastPosition.X + (newPosition.X - startPositionPan.X) * panMultiplier;
                _translateTransform.Y = lastPosition.Y + (newPosition.Y - startPositionPan.Y) * panMultiplier;

                // changing the background grid

                // creates a new translate transform which is basically the same as the other one
                // but it doesnt multiply by panMultiplier, otherwise it would be too fast
                TranslateTransform gridTransform = new TranslateTransform();
                gridTransform.X = lastPosition.X + (newPosition.X - startPositionPan.X);
                gridTransform.Y = lastPosition.Y + (newPosition.Y - startPositionPan.Y);

                // then its just set to the transform
                _backgroundGrid.Transform = new MatrixTransform(gridTransform.Value);
            }
        }
        #endregion

        public void UnhideButton_Click(object sender, RoutedEventArgs e)
        {
            // loops through each canvas in _uiCanvas and sets them all visible
            foreach (Canvas canvas in _uiCanvas.Children)
            {
                canvas.Visibility = Visibility.Visible;
            }
        }

        // switches between the roundTo value and 1 which changes how precise ui dragging is
        // idk why i didn't just call it "Snapping" lol
        private bool isPrecise = true;

        public void PreciseButton_Click(object sender, RoutedEventArgs e)
        {
            // toggles the bool
            isPrecise = !isPrecise;

            // gets the icon for the off and on icons
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

        // refreshes the ui editor
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // clears all the dictionaries for the textures
            MappingIdToMapping.Clear();
            MappingMinValue.Clear();
            MappingMaxValue.Clear();
            MappingTexture.Clear();

            // clears everything in the ui canvas
            _uiCanvas.Children.Clear();

            // reloads all the ui
            EbxAssetEntry openedAsset = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;
            LoadUI(openedAsset, false, null);

            _uiCanvas.UpdateLayout();

            _tabControl.SelectedIndex = 0;

            App.Logger.Log("Refreshed UI");
        }
    }
}
