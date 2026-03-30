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
        private dynamic uiComponent;

        public double actualWidth;
        public double actualHeight;

        private double positionX;
        private double positionY;

        public UIBlueprintElement(dynamic uiComponent, bool widget, Movement movement)
        {
            this.uiComponent = uiComponent;

            // positioning / sizing
            dynamic rootObject = CurrentRootObject.Get();

            double mainSizeX = rootObject.Object.Internal.Size.X;
            double mainSizeY = rootObject.Object.Internal.Size.Y;

            double offsetX = uiComponent.Internal.Offset.X;
            double offsetY = uiComponent.Internal.Offset.Y;
            double anchorX = uiComponent.Internal.Anchor.X;
            double anchorY = uiComponent.Internal.Anchor.Y;

            double sizeX = uiComponent.Internal.Size.X;
            double sizeY = uiComponent.Internal.Size.Y;

            // these are the positions used for almost every ui element we'll create
            double finalX = anchorX * (mainSizeX - sizeX) + offsetX;
            double finalY = anchorY * (mainSizeY - sizeY) + offsetY;
            positionX = finalX;
            positionY = finalY;

            // if ShowAllUI is true, it will also include elements that have an alpha of 0
            if (uiComponent.Internal.Alpha != null)
            {
                bool ShowAllUI = Config.Get("ShowAllUI", false);

                Opacity = ShowAllUI ? 1 : uiComponent.Internal.Alpha;
            }

            actualWidth = sizeX;
            actualHeight = sizeY;

            // if the size is negative it will return the absolute value to make sure it's not negative
            // otherwise it will just throw an exception
            Width = sizeX < 0 ? Math.Abs(sizeX) : sizeX;
            Height = sizeY < 0 ? Math.Abs(sizeY) : sizeY;
            Tag = uiComponent.Internal.__InstanceGuid;

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
            double rotation = uiComponent.Internal.UIElementTransform.Rotation.z;

            double rotationPivotX = uiComponent.Internal.UIElementTransform.RotationPivot.x;
            double rotationPivotY = uiComponent.Internal.UIElementTransform.RotationPivot.y;

            var transformGroupCanvas = new TransformGroup();

            var rotateTransform = new RotateTransform(rotation);
            rotateTransform.CenterX = rotationPivotX;
            rotateTransform.CenterY = rotationPivotY;

            transformGroupCanvas.Children.Add(rotateTransform);

            canvas.RenderTransform = transformGroupCanvas;
        }
    }
}
