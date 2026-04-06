using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using UIBlueprintEditor.Editor.Misc;

namespace UIBlueprintEditor.Editor.UI
{
    // a class that represents a ui element and is displayed on the main ui canvas (in UIEditor.cs)
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

            // positioning / sizing

            double mainSizeX = rootObject.Object.Internal.Size.X;
            double mainSizeY = rootObject.Object.Internal.Size.Y;

            double offsetX = uiElement.Internal.Offset.X;
            double offsetY = uiElement.Internal.Offset.Y;
            double anchorX = uiElement.Internal.Anchor.X;
            double anchorY = uiElement.Internal.Anchor.Y;

            double sizeX = uiElement.Internal.Size.X;
            double sizeY = uiElement.Internal.Size.Y;

            // these are the positions used for almost every ui element we'll create
            double finalX = anchorX * (mainSizeX - sizeX) + offsetX;
            double finalY = anchorY * (mainSizeY - sizeY) + offsetY;
            PositionX = finalX;
            PositionY = finalY;

            // if ShowAllUI is true, it will also include elements that have an alpha of 0
            if (uiElement.Internal.Alpha != null)
            {
                bool ShowAllUI = Config.Get("ShowAllUI", false);

                Opacity = ShowAllUI ? 1 : uiElement.Internal.Alpha;
            }

            OriginalWidth = sizeX;
            OriginalHeight = sizeY;

            // if the size is negative it will return the absolute value to make sure it's not negative
            // otherwise it will just throw an exception
            Width = sizeX < 0 ? Math.Abs(sizeX) : sizeX;
            Height = sizeY < 0 ? Math.Abs(sizeY) : sizeY;
            Tag = uiElement.Internal.__InstanceGuid;

            RotateElement(this);

            SetLeft(this, finalX);
            SetTop(this, finalY);

            // lets you control the element if its not a widget reference
            if (!widget)
            {
                movement.ControlUI(this);
            }
        }

        private void RotateElement(Canvas canvas)
        {
            // the rotation is an xyz value but it seems like x and y just warps it
            // so only z is used
            double rotation = _uiElement.Internal.UIElementTransform.Rotation.z;

            double rotationPivotX = _uiElement.Internal.UIElementTransform.RotationPivot.x;
            double rotationPivotY = _uiElement.Internal.UIElementTransform.RotationPivot.y;

            var transformGroupCanvas = new TransformGroup();

            var rotateTransform = new RotateTransform(rotation);
            rotateTransform.CenterX = rotationPivotX;
            rotateTransform.CenterY = rotationPivotY;

            transformGroupCanvas.Children.Add(rotateTransform);

            canvas.RenderTransform = transformGroupCanvas;
        }
    }
}
