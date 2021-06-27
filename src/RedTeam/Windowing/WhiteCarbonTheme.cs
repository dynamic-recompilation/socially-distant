using System.Numerics;
using RedTeam.Core.Windowing;
using Thundershock.Core;
using Thundershock.Core.Rendering;
using Thundershock.Gui;

namespace RedTeam.Windowing
{
    public class WhiteCarbonTheme : WindowTheme
    {
        private GraphicsProcessor _gpu;
        private int _borderPad = 3;

        private Font _titleFont;
        
        private Texture2D _topLeftCorner;
        private Texture2D _topRightCorner;
        private Texture2D _bottomLeftCorner;
        private Texture2D _bottomRightCorner;
        private Texture2D _rightSide;
        private Texture2D _leftSide;
        private Texture2D _bottomSide;
        private Texture2D _topSide;
        private Texture2D _titleLeft;
        private Texture2D _titleRight;
        
        protected override void OnLoad(GraphicsProcessor gpu)
        {
            _gpu = gpu;
            
            _topLeftCorner = LoadResource("WindowCornerTL.png");
            _topRightCorner = LoadResource("WindowCornerTR.png");
            _bottomRightCorner = LoadResource("WindowCornerBR.png");
            _bottomLeftCorner = LoadResource("WindowCornerBL.png");
            
            _rightSide = LoadResource("WindowSideR.png");
            _leftSide = LoadResource("WindowSideL.png");
            _bottomSide = LoadResource("WindowSideB.png");
            _topSide = LoadResource("WindowSideT.png");

            _titleLeft = LoadResource("WindowTitleLeft.png");
            _titleRight = LoadResource("WindowTitleRight.png");
            
            
            // TODO: don't use the engine font for this.
            _titleFont = Font.GetDefaultFont(_gpu);
            _titleFont.Size = _titleLeft.Height;
            
            base.OnLoad(gpu);
        }

        public override Padding GetClientPadding(WindowFrame win)
        {
            return new Padding(
                _leftSide.Width + _borderPad,
                _topSide.Height + _borderPad,
                _rightSide.Width + _borderPad,
                _bottomSide.Height + _borderPad
            );
        }

        public override void PaintWindow(GameTime gameTime, GuiRenderer renderer, WindowFrame win)
        {
            // black BG for the main window bg
            renderer.FillRectangle(win.BoundingBox, Color.Black);
            
            var client = win.BoundingBox;
            var padding = GetClientPadding(win);
            client.X += padding.Left;
            client.Y += padding.Top;
            client.Width -= padding.Width;
            client.Height -= padding.Height;

            // top-left corner
            renderer.FillRectangle(
                new Rectangle(
                    win.BoundingBox.Left,
                    win.BoundingBox.Top,
                    _topLeftCorner.Width,
                    _topLeftCorner.Height
                ),
                _topLeftCorner,
                Color.White
            );
            
            // top-right corner
            renderer.FillRectangle(
                new Rectangle(
                    win.BoundingBox.Right - _topRightCorner.Width,
                    win.BoundingBox.Top,
                    _topRightCorner.Width,
                    _topRightCorner.Height
                ),
                _topRightCorner,
                Color.White
            );
            
            // bottom-left corner
            renderer.FillRectangle(
                new Rectangle(
                    win.BoundingBox.Left,
                    win.BoundingBox.Bottom - _bottomLeftCorner.Height,
                    _bottomLeftCorner.Width,
                    _bottomLeftCorner.Height
                ),
                _bottomLeftCorner,
                Color.White
            );
            
            // bottom-right corner
            renderer.FillRectangle(
                new Rectangle(
                    win.BoundingBox.Right - _bottomRightCorner.Width,
                    win.BoundingBox.Bottom - _bottomRightCorner.Height,
                    _bottomRightCorner.Width,
                    _bottomRightCorner.Height
                ),
                _bottomRightCorner,
                Color.White
            );
            
            // Left side
            renderer.FillRectangle(new Rectangle(
                    win.BoundingBox.Left + _borderPad,
                    win.BoundingBox.Top + _topLeftCorner.Height,
                    _leftSide.Width,
                    win.BoundingBox.Height - (_topLeftCorner.Height + _bottomLeftCorner.Height)
                ),
                _leftSide,
                Color.White);
            
            // Right side
            renderer.FillRectangle(new Rectangle(
                    win.BoundingBox.Right - (_rightSide.Width + _borderPad),
                    win.BoundingBox.Top + _topRightCorner.Height,
                    _rightSide.Width,
                    win.BoundingBox.Height - (_topRightCorner.Height + _bottomRightCorner.Height)
                ),
                _rightSide,
                Color.White);
            
            // Bottom side
            renderer.FillRectangle(new Rectangle(
                    win.BoundingBox.Left + _bottomLeftCorner.Width,
                    win.BoundingBox.Bottom - (_bottomSide.Height + _borderPad),
                    win.BoundingBox.Width - (_bottomLeftCorner.Width + _bottomRightCorner.Width),
                    _bottomSide.Height
                ),
                _bottomSide,
                Color.White);
            
            // Top side
            renderer.FillRectangle(new Rectangle(
                    win.BoundingBox.Left + _topLeftCorner.Width,
                    win.BoundingBox.Top + _borderPad,
                    win.BoundingBox.Width - (_topLeftCorner.Width + _topRightCorner.Width),
                    _topSide.Height
                ),
                _topSide,
                Color.White);
            
            

            // So for the title text we need to do something a bit different.
            // If we don't have any title text to render then we're just not going to render
            // the title textures (because it'll look weird if we do with this theme.)
            if (!string.IsNullOrWhiteSpace(win.TitleText))
            {
                // We will need to measure the text to get its pixel width.
                var textWidth = _titleFont.MeasureString(win.TitleText).X;
                
                // Then we need to render a black box where the text and title textures will go.
                // That way we don't have the title bar side texture striking through the text.
                var textRect = new Rectangle(
                    win.BoundingBox.Left + _topLeftCorner.Width + (_borderPad * 2),
                    win.BoundingBox.Top,
                    _titleLeft.Width + textWidth + _titleRight.Width,
                    _titleFont.Size
                );
                
                // Render that black box.
                renderer.FillRectangle(textRect, Color.Black);
                
                // Now that we've done that, we can now render the title area sides.
                var leftSiderect = textRect;
                leftSiderect.Width = _titleLeft.Width;

                var rightSideRect = textRect;
                rightSideRect.X = rightSideRect.Right - _titleRight.Width;
                rightSideRect.Width = _titleRight.Width;

                renderer.FillRectangle(leftSiderect, _titleLeft, Color.White);
                renderer.FillRectangle(rightSideRect, _titleRight, Color.White);
                
                // Now we can paint the text.
                renderer.DrawString(_titleFont, win.TitleText, new Vector2(leftSiderect.Right, leftSiderect.Top),
                    Color.White);
            }
            
            // Now we paint the client area.
            renderer.FillRectangle(client, Color.Black);
        }

        private Texture2D LoadResource(string resource)
        {
            var ass = GetType().Assembly;
            var fullname = "RedTeam.Resources.ThemeAssets.WhiteCarbon." + resource;
            return Texture2D.FromResource(_gpu, ass, fullname);
        }
    }
}