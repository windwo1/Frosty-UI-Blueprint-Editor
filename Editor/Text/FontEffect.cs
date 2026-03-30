using Frosty.Core;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace UIBlueprintEditor.Editor.Text
{
    public class FontEffect
    {
        // info on effects are in this google doc: https://docs.google.com/document/d/1EdNMCM0jUy4g_uLQMIZm5RP2XCep35ui5dGl61VXOTc/edit?usp=sharing
        // credits to brekko for giving me a txt file for it, i just put it in a google doc so it's easier to read
        public void Apply(TextBlock tb, Border border, Canvas canvas, EbxAssetEntry fontEffectEbx)
        {
            // most effects aren't used or don't make much of a difference so
            // there's no point of coding it in, so only these font effects will have an effect on text
            string[] effectWhitelist = { "SetGlyphColor", "SetGlyphOffset", "SetGlyphBrush", "DrawGlyph", "DrawGlyphSmearOutline", "Merge", "Clear" };

            EbxAsset fontEffectAsset = App.AssetManager.GetEbx(fontEffectEbx);
            dynamic rootObjectFontEffect = fontEffectAsset.RootObject;

            string fontEffect = rootObjectFontEffect.EffectScript;

            using (StringReader reader = new StringReader(fontEffect))
            {
                Dictionary<string, string> currentValues = new Dictionary<string, string>();

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] effectArray = line.Split(' ');

                    string effectName = effectArray[0];

                    if (effectWhitelist.Contains(effectName))
                    {
                        try
                        {
                            if (effectArray.Length == 1) // if the effect has no arguments (for example: Merge, Clear...)
                            {
                                switch (effectName)
                                {
                                    case "DrawGlyph":
                                        // draws the text

                                        if (currentValues.ContainsKey("SetGlyphColor"))
                                        {
                                            tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(currentValues["SetGlyphColor"]));
                                        }
                                        break;
                                    case "DrawGlyphSmearOutline":
                                        // draws an outline

                                        SolidColorBrush color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(currentValues["SetGlyphColor"]));

                                        // 'AdornerLayer.GetAdornerLayer()' will return null if this isn't used
                                        var adornerLayer = new AdornerDecorator
                                        {
                                            ClipToBounds = true,
                                        };

                                        // this basically copies everything that was done when first rendering the text field
                                        var borderStroke = new Border
                                        {
                                            Width = border.Width,
                                            Height = border.Height,
                                        };

                                        var stroke = new TextBlock
                                        {
                                            Text = tb.Text,
                                            Margin = tb.Margin,
                                            Opacity = tb.Opacity,
                                            Padding = tb.Padding,
                                            TextWrapping = tb.TextWrapping,
                                            FontFamily = tb.FontFamily,
                                            FontSize = tb.FontSize,
                                            TextAlignment = tb.TextAlignment,
                                            VerticalAlignment = tb.VerticalAlignment,
                                            RenderTransform = tb.RenderTransform,
                                        };

                                        // sets it below the tb
                                        Panel.SetZIndex(stroke, Panel.GetZIndex(tb) - 1);

                                        // sets an offset if it exists
                                        if (currentValues.ContainsKey("SetGlyphOffset"))
                                        {
                                            string[] offset = currentValues["SetGlyphOffset"].Split(',');

                                            double x = Convert.ToDouble(offset[0]);
                                            double y = Convert.ToDouble(offset[1]);

                                            borderStroke.Margin = new Thickness(x, y, 0, 0);
                                        }

                                        // if there is no "SetGlyphBrush" it will use 5 for the default thickness
                                        ushort thickness = 5;
                                        if (currentValues.ContainsKey("SetGlyphBrush"))
                                        {
                                            // dividing by 1.2 seems about right, otherwise some strokes are too big
                                            double brush = Convert.ToDouble(currentValues["SetGlyphBrush"]) / 1.2;

                                            thickness = (ushort)brush;
                                        }

                                        canvas.Children.Add(adornerLayer);
                                        adornerLayer.Child = borderStroke;
                                        borderStroke.Child = stroke;

                                        AdornerLayer adorner = AdornerLayer.GetAdornerLayer(stroke);

                                        StrokeAdorner strokeAdorner = new StrokeAdorner(stroke);

                                        // this is so the opacity of the text block is included
                                        var colorWithAlpha = new SolidColorBrush(Color.FromArgb((byte)Math.Round(tb.Opacity * 255), color.Color.R, color.Color.G, color.Color.B));

                                        strokeAdorner.Stroke = colorWithAlpha;
                                        strokeAdorner.StrokeThickness = thickness;
                                        strokeAdorner.Fill = colorWithAlpha;

                                        adorner.Add(strokeAdorner);
                                        break;
                                    case "Merge":
                                        // moves onto the next part

                                        currentValues.Clear();
                                        break;
                                    case "Clear":
                                        // the same thing as Merge, this is only here because some font effects skip Merge

                                        currentValues.Clear();
                                        break;
                                }
                            }
                            else
                            {
                                // these are the arugments for each effect (most of the time there is only one)
                                // the first index is skipped because that is just the name of the effect
                                string[] effectValues = effectArray.Skip(1).ToArray();

                                switch (effectName)
                                {
                                    case "SetGlyphColor":
                                        string value = effectValues[0];

                                        // removes the first 4 characters (0xff) and puts a '#' before it
                                        // sometimes the hex doesnt include the 'ff' after '0x' so only 2 is cut off
                                        string fullHex = value.Remove(0, value.Length > 8 ? 4 : 2).Insert(0, "#");

                                        // this will limit the hex from being longer than 7 (not 6 because the '#' is included)
                                        string hex = fullHex.Remove(7, fullHex.Length - 7);

                                        currentValues.Add("SetGlyphColor", hex);
                                        break;
                                    case "SetGlyphOffset":
                                        string offset = $"{effectValues[0]},{effectValues[1]}";

                                        currentValues.Add("SetGlyphOffset", offset);
                                        break;
                                    case "SetGlyphBrush":
                                        // this gets the second value (index 1) for the size, there are actually 4 arguments
                                        // but we only really need the size. the full arguments are: uint32_t shape, size, hardness, opacity

                                        string size = effectValues[1];

                                        currentValues.Add("SetGlyphBrush", size);
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Log($"Error loading the Font Effect {fontEffectEbx.Name}: {ex}");
                        }
                    }
                }
            }
        }
    }
}
