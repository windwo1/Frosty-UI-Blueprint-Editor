using Frosty.Core;
using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace UIBlueprintEditor.Editor.UI
{
    // a class that represents a ui element and is displayed on the main ui canvas
    public class UIBlueprintElement : Canvas
    {
        public double OriginalWidth;
        public double OriginalHeight;

        public double PositionX;
        public double PositionY;

        private dynamic _uiElement;

        public UIBlueprintElement(dynamic uiElement, bool widget, Movement movement, dynamic rootObject)
        {
            _uiElement = uiElement;

            double mainSizeX = rootObject.Object.Internal.Size.X;
            double mainSizeY = rootObject.Object.Internal.Size.Y;

            double offsetX = uiElement.Internal.Offset.X;
            double offsetY = uiElement.Internal.Offset.Y;
            double anchorX = uiElement.Internal.Anchor.X;
            double anchorY = uiElement.Internal.Anchor.Y;

            double sizeX = uiElement.Internal.Size.X;
            double sizeY = uiElement.Internal.Size.Y;

            double finalX = anchorX * (mainSizeX - sizeX) + offsetX;
            double finalY = anchorY * (mainSizeY - sizeY) + offsetY;
            PositionX = finalX;
            PositionY = finalY;

            if (uiElement.Internal.Alpha != null)
            {
                bool ShowAllUI = Config.Get("ShowAllUI", false);

                Opacity = ShowAllUI ? 1 : uiElement.Internal.Alpha;
            }

            OriginalWidth = sizeX;
            OriginalHeight = sizeY;

            // if the size is negative it will return the absolute value to make sure it's not negative
            Width = sizeX < 0 ? Math.Abs(sizeX) : sizeX;
            Height = sizeY < 0 ? Math.Abs(sizeY) : sizeY;
            Tag = uiElement.Internal.__InstanceGuid;

            // the rotation is an xyz value but it seems like x and y just warps it so only z is used
            double rotation = _uiElement.Internal.UIElementTransform.Rotation.z;

            double rotationPivotX = _uiElement.Internal.UIElementTransform.RotationPivot.x;
            double rotationPivotY = _uiElement.Internal.UIElementTransform.RotationPivot.y;

            var transformGroupCanvas = new TransformGroup();

            var rotateTransform = new RotateTransform(rotation);
            var scaleTransform = new ScaleTransform();

            string transformString;
            try
            {
                transformString = _uiElement.Internal.Transform.ToString();
            }
            catch
            {
                transformString = _uiElement.Internal.BlueprintTransform.ToString();
            }

            // the transform values for some reason are all either just "3.402823E+38" or 0
            // so i think this needs to be done
            string[] values = transformString.Replace(" ", "/").Replace("(", "").Replace(")", "").Split('/');
            scaleTransform.ScaleX = Convert.ToDouble(values[3]);
            scaleTransform.ScaleY = Convert.ToDouble(values[4]);

            transformGroupCanvas.Children.Add(rotateTransform);
            transformGroupCanvas.Children.Add(scaleTransform);

            RenderTransform = transformGroupCanvas;
            RenderTransformOrigin = new System.Windows.Point(rotationPivotX, rotationPivotY);

            SetLeft(this, finalX);
            SetTop(this, finalY);

            if (!widget)
            {
                movement.ControlUI(this);
            }
        }
    }
}
