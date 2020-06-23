﻿// uncomment line below to show debug values
#define SIMULATOR_DEBUG_INFO


using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Objects;
using HaCreator.MapSimulator.Objects.FieldObject;
using HaCreator.MapSimulator.Objects.UIObject;
using HaSharedLibrary;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace HaCreator.MapSimulator
{
    /// <summary>
    /// 
    /// http://rbwhitaker.wikidot.com/xna-tutorials
    /// </summary>
    public class MapSimulator : Microsoft.Xna.Framework.Game
    {
        public int mapShiftX = 0;
        public int mapShiftY = 0;
        public Point minimapPos;

        private int RenderWidth;
        private int RenderHeight;
        private float RenderObjectScaling = 1.0f;
        private MapRenderResolution mapRenderResolution;

        private GraphicsDeviceManager _DxDeviceManager;
        private readonly TexturePool texturePool = new TexturePool();
        private bool bSaveScreenshot = false, bSaveScreenshotComplete = true; // flag for saving a screenshot file

        private SpriteBatch spriteBatch;

        // Objects, NPCs
        public List<BaseItem>[] mapObjects;
        private readonly List<BaseItem> mapObjects_NPCs = new List<BaseItem>();
        private readonly List<BaseItem> mapObjects_Mobs = new List<BaseItem>();
        private readonly List<BaseItem> mapObjects_Reactors = new List<BaseItem>();
        private readonly List<BaseItem> mapObjects_Portal = new List<BaseItem>(); // perhaps mapobjects should be in a single pool

        // Backgrounds
        private readonly List<BackgroundItem> backgrounds_front = new List<BackgroundItem>();
        private readonly List<BackgroundItem> backgrounds_back = new List<BackgroundItem>();

        // Boundary, borders
        private Rectangle vr_fieldBoundary;

        // Minimap
        private Texture2D texture_miniMapPixel;
        private Texture2D texturer_miniMap;

        // Cursor, mouse
        private MouseCursorItem mouseCursor;

        // Audio
        private WzMp3Streamer audio;

        // Etc
        private readonly Board mapBoard;

        // Spine
        private SkeletonMeshRenderer skeletonMeshRenderer;

        // Text
        private SpriteFont font_navigationKeysHelper;
        private SpriteFont font_DebugValues;

        /// <summary>
        /// MapSimulator Constructor
        /// </summary>
        /// <param name="mapBoard"></param>
        /// <param name="titleName"></param>
        public MapSimulator(Board mapBoard, string titleName)
        {
            this.mapBoard = mapBoard;

            this.mapRenderResolution = UserSettings.SimulateResolution;
            InitialiseMapWidthHeight();

            //RenderHeight += System.Windows.Forms.SystemInformation.CaptionHeight; // window title height

            //double dpi = ScreenDPIUtil.GetScreenScaleFactor();

            // set Form window height & width
            //this.Width = (int)(RenderWidth * dpi);
            //this.Height = (int)(RenderHeight * dpi);
            
            //Window.IsBorderless = true;
            //Window.Position = new Point(0, 0);
            Window.Title = titleName;
            IsFixedTimeStep = false; // dont cap fps
            IsMouseVisible = false; // draws our own custom cursor here.. 

            _DxDeviceManager = new GraphicsDeviceManager(this)
            {
                SynchronizeWithVerticalRetrace = true, 
                HardwareModeSwitch = true,
                GraphicsProfile = GraphicsProfile.HiDef,
                IsFullScreen = false,
                PreferMultiSampling = true,
                SupportedOrientations = DisplayOrientation.Default,
                PreferredBackBufferWidth = Math.Max(RenderWidth, 1),
                PreferredBackBufferHeight = Math.Max(RenderHeight, 1),
                PreferredBackBufferFormat = SurfaceFormat.Color,
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            };
            _DxDeviceManager.ApplyChanges();

        }

        #region Loading and unloading
        private void InitialiseMapWidthHeight()
        {
            RenderObjectScaling = 1.0f;
            switch (this.mapRenderResolution)
            {
                case MapRenderResolution.Res_1024x768:  // 1024x768
                    RenderHeight = 768;
                    RenderWidth = 1024;
                    break;
                case MapRenderResolution.Res_1280x720: // 1280x720
                    RenderHeight = 720;
                    RenderWidth = 1280;
                    break;
                case MapRenderResolution.Res_1366x768:  // 1366x768
                    RenderHeight = 768;
                    RenderWidth = 1366;
                    break;


                case MapRenderResolution.Res_1920x1080: // 1920x1080
                    RenderHeight = 1080;
                    RenderWidth = 1920;
                    break;
                case MapRenderResolution.Res_1920x1080_120PercScaled: // 1920x1080
                    RenderHeight = 1080;
                    RenderWidth = 1920;
                    RenderObjectScaling = 1.2f;
                    break;
                case MapRenderResolution.Res_1920x1080_150PercScaled: // 1920x1080
                    RenderHeight = 1080;
                    RenderWidth = 1920;
                    RenderObjectScaling = 1.5f;
                    this.mapRenderResolution |= MapRenderResolution.Res_1366x768; // 1920x1080 is just 1366x768 with 150% scale.
                    break;


                case MapRenderResolution.Res_1920x1200: // 1920x1200
                    RenderHeight = 1200;
                    RenderWidth = 1920;
                    break;
                case MapRenderResolution.Res_1920x1200_120PercScaled: // 1920x1200
                    RenderHeight = 1200;
                    RenderWidth = 1920;
                    RenderObjectScaling = 1.2f;
                    break;
                case MapRenderResolution.Res_1920x1200_150PercScaled: // 1920x1200
                    RenderHeight = 1200;
                    RenderWidth = 1920;
                    RenderObjectScaling = 1.5f;
                    break;

                case MapRenderResolution.Res_All:
                case MapRenderResolution.Res_800x600: // 800x600
                default:
                    RenderHeight = 600;
                    RenderWidth = 800;
                    break;
            }
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            // Create map layers
            mapObjects = new List<BaseItem>[WzConstants.MaxMapLayers];
            for (int i = 0; i < WzConstants.MaxMapLayers; i++)
            {
                mapObjects[i] = new List<BaseItem>();
            }

            //GraphicsDevice.Viewport = new Viewport(RenderWidth / 2 - 800 / 2, RenderHeight / 2 - 600 / 2, 800, 600);

            // https://stackoverflow.com/questions/55045066/how-do-i-convert-a-ttf-or-other-font-to-a-xnb-xna-game-studio-font
            // if you're having issues building on w10, install Visual C++ Redistributable for Visual Studio 2012 Update 4
            // 
            // to build your own font: /MonoGame Font Builder/game.mgcb
            // build -> obj -> copy it over to HaRepacker-resurrected [Content]
            font_navigationKeysHelper = Content.Load<SpriteFont>("XnaDefaultFont");
            font_DebugValues = Content.Load<SpriteFont>("XnaFont_Debug");

            base.Initialize();
        }

        /// <summary>
        /// Load game assets
        /// </summary>
        protected override void LoadContent()
        {
            WzDirectory MapWzFile = Program.WzManager["map"]; // Map.wz
            WzDirectory UIWZFile = Program.WzManager["ui"];

            // BGM
            if (Program.InfoManager.BGMs.ContainsKey(mapBoard.MapInfo.bgm))
            {
                audio = new WzMp3Streamer(Program.InfoManager.BGMs[mapBoard.MapInfo.bgm], true);
                if (audio != null)
                {
                    audio.Volume = 0.3f;
                    audio.Play();
                }
            }
            if (mapBoard.VRRectangle == null)
                vr_fieldBoundary = new Rectangle(0, 0, mapBoard.MapSize.X, mapBoard.MapSize.Y);
            else
                vr_fieldBoundary = new Rectangle(mapBoard.VRRectangle.X + mapBoard.CenterPoint.X, mapBoard.VRRectangle.Y + mapBoard.CenterPoint.Y, mapBoard.VRRectangle.Width, mapBoard.VRRectangle.Height);
            //SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);

            // Background and objects
            List<WzObject> usedProps = new List<WzObject>();

            foreach (LayeredItem tileObj in mapBoard.BoardItems.TileObjs)
            {
                WzImageProperty tileParent = (WzImageProperty)tileObj.BaseInfo.ParentObject;

                mapObjects[tileObj.LayerNumber].Add(
                    MapSimulatorLoader.CreateMapItemFromProperty(texturePool, tileParent, tileObj.X, tileObj.Y, mapBoard.CenterPoint, _DxDeviceManager.GraphicsDevice, ref usedProps, tileObj is IFlippable ? ((IFlippable)tileObj).Flip : false));
            }
            foreach (BackgroundInstance background in mapBoard.BoardItems.BackBackgrounds)
            {
                WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;

                backgrounds_back.Add(
                    MapSimulatorLoader.CreateBackgroundFromProperty(texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip));
            }
            foreach (BackgroundInstance background in mapBoard.BoardItems.FrontBackgrounds)
            {
                WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;

                backgrounds_front.Add(
                    MapSimulatorLoader.CreateBackgroundFromProperty(texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip));
            }

            // Load reactors
            foreach (ReactorInstance reactor in mapBoard.BoardItems.Reactors)
            {
                //WzImage imageProperty = (WzImage)NPCWZFile[reactorInfo.ID + ".img"];

                ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(texturePool, reactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                mapObjects_Reactors.Add(reactorItem);
            }

            // Load NPCs
            foreach (NpcInstance npc in mapBoard.BoardItems.NPCs)
            {
                //WzImage imageProperty = (WzImage) NPCWZFile[npcInfo.ID + ".img"];

                NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(texturePool, npc, _DxDeviceManager.GraphicsDevice, ref usedProps);
                mapObjects_NPCs.Add(npcItem);
            }
            // Load Mobs
            foreach (MobInstance mob in mapBoard.BoardItems.Mobs)
            {
                //WzImage imageProperty = Program.WzManager.FindMobImage(mobInfo.ID); // Mob.wz Mob2.img Mob001.wz

                MobItem npcItem = MapSimulatorLoader.CreateMobFromProperty(texturePool, mob, _DxDeviceManager.GraphicsDevice, ref usedProps);
                mapObjects_Mobs.Add(npcItem);
            }

            // Load portals
            WzSubProperty portalParent = (WzSubProperty) MapWzFile["MapHelper.img"]["portal"];

            WzSubProperty gameParent = (WzSubProperty) portalParent["game"];
            //WzSubProperty editorParent = (WzSubProperty) portalParent["editor"];

            foreach (PortalInstance portal in mapBoard.BoardItems.Portals)
            {
                PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(texturePool, gameParent, portal, _DxDeviceManager.GraphicsDevice, ref usedProps);
                if (portalItem != null) 
                    mapObjects_Portal.Add(portalItem);
            }

            // Cursor
            WzImageProperty cursorImageProperty = (WzImageProperty)UIWZFile["Basic.img"]?["Cursor"];
            this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(texturePool, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, ref usedProps, false);

            // Spine object
            skeletonMeshRenderer = new SkeletonMeshRenderer(GraphicsDevice)
            {
                PremultipliedAlpha = false
            };

            // Minimap
            minimapPos = new Point((int)Math.Round((mapBoard.MinimapPosition.X + mapBoard.CenterPoint.X) / (double)mapBoard.mag), (int)Math.Round((mapBoard.MinimapPosition.Y + mapBoard.CenterPoint.Y) / (double)mapBoard.mag));
            this.texturer_miniMap = BoardItem.TextureFromBitmap(GraphicsDevice, mapBoard.MiniMap);

            //
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(1, 1);
            bmp.SetPixel(0, 0, System.Drawing.Color.White);
            texture_miniMapPixel = BoardItem.TextureFromBitmap(GraphicsDevice, bmp);

            spriteBatch = new SpriteBatch(GraphicsDevice);

            // default positioning for character
            SetCameraMoveX(true, true, 0);
            SetCameraMoveY(true, true, 0);

            // cleanup
            // clear used items
            foreach (WzObject obj in usedProps)
            {
                obj.MSTag = null;
                obj.MSTagSpine = null; // cleanup
            }
            usedProps.Clear();

        }

        protected override void UnloadContent()
        {
            if (audio != null)
            {
                //audio.Pause();
                audio.Dispose();
            }

            skeletonMeshRenderer.End();
            
            _DxDeviceManager.EndDraw();
            _DxDeviceManager.Dispose();


            mapObjects_NPCs.Clear();
            mapObjects_Mobs.Clear();
            mapObjects_Reactors.Clear();
            mapObjects_Portal.Clear();

            backgrounds_front.Clear();
            backgrounds_back.Clear();

            texturePool.Dispose();
        }
        #endregion

        #region Update and Drawing
        /// <summary>
        /// Key handling
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(GameTime gameTime)
        {
            float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
            int TickCount = Environment.TickCount;
            float delta = gameTime.ElapsedGameTime.Milliseconds / 1000f;

            // Allows the game to exit
#if !WINDOWS_STOREAPP
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                this.Exit();
                return;
            }
#endif
            // Handle full screen
            bool bIsAltEnterPressed = Keyboard.GetState().IsKeyDown(Keys.LeftAlt) && Keyboard.GetState().IsKeyDown(Keys.Enter);
            if (bIsAltEnterPressed)
            {
                _DxDeviceManager.IsFullScreen = !_DxDeviceManager.IsFullScreen;
                _DxDeviceManager.ApplyChanges();
                return;
            }

            // Handle print screen
            if (Keyboard.GetState().IsKeyDown(Keys.PrintScreen))
            {
                if (!bSaveScreenshot && bSaveScreenshotComplete)
                {
                    this.bSaveScreenshot = true; // flag for screenshot
                    this.bSaveScreenshotComplete = false;
                }
            }


            // Handle mouse
            mouseCursor.UpdateCursorState();


            // Navigate around the rendered object
            bool bIsShiftPressed = Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);

            bool bIsUpKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Up);
            bool bIsDownKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Down);
            bool bIsLeftKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Left);
            bool bIsRightKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Right);

            int moveOffset = bIsShiftPressed ? (int) (3000f / frameRate) : (int) (1500f / frameRate); // move a fixed amount a second, not dependent on GPU speed
            if (bIsLeftKeyPressed || bIsRightKeyPressed)
            {
                SetCameraMoveX(bIsLeftKeyPressed, bIsRightKeyPressed, moveOffset);
            }
            if (bIsUpKeyPressed || bIsDownKeyPressed)
            {
                SetCameraMoveY(bIsUpKeyPressed, bIsDownKeyPressed, moveOffset);
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
            int TickCount = Environment.TickCount;
            float delta = gameTime.ElapsedGameTime.Milliseconds / 1000f;


            //GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 1.0f, 0); // Clear the window to black
            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(
                SpriteSortMode.Immediate, // spine :( needs to be drawn immediately to maintain the layer orders
                //SpriteSortMode.Deferred,
                BlendState.NonPremultiplied, null, null, null, null, Matrix.CreateScale(RenderObjectScaling));
            //skeletonMeshRenderer.Begin();

            // Back Backgrounds
            backgrounds_back.ForEach(bg =>
            {
                bg.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                    TickCount);
            });

            // Map objects
            foreach (List<BaseItem> mapItem in mapObjects)
            {
                foreach (BaseItem item in mapItem)
                {
                    item.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                        mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                        RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                        TickCount);
                }
            }
            // Portals
            foreach (PortalItem portalItem in mapObjects_Portal)
            {
                portalItem.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                    TickCount);
            }

            // Reactors
            foreach (ReactorItem reactorItem in mapObjects_Reactors)
            {
                reactorItem.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                    TickCount);
            }

            // Life (NPC + Mobs)
            foreach (MobItem mapMob in mapObjects_Mobs) // Mobs
            {
                mapMob.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                    TickCount);
            }
            foreach (NpcItem mapNpc in mapObjects_NPCs) // NPCs (always in front of mobs)
            {
                mapNpc.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                    TickCount);
            }

            // Front Backgrounds
            backgrounds_front.ForEach(bg =>
            {
                bg.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                    TickCount);
            });

            // Borders
            // Create any rectangle you want. Here we'll use the TitleSafeArea for fun.
            //Rectangle titleSafeRectangle = GraphicsDevice.Viewport.TitleSafeArea;
            //DrawBorder(spriteBatch, titleSafeRectangle, 1, Color.Black);

            // UI related here
            // Minimap
            if (texturer_miniMap != null)
            {
                spriteBatch.Draw(texturer_miniMap, new Rectangle(minimapPos.X, minimapPos.Y, texturer_miniMap.Width, texturer_miniMap.Height), Color.White);
                int minimapPosX = (mapShiftX + (RenderWidth / 2)) / 16;
                int minimapPosY = (mapShiftY + (RenderHeight / 2)) / 16;

                FillRectangle(spriteBatch, new Rectangle(minimapPosX - 4, minimapPosY - 4, 4, 4), Color.Yellow);
            }


            if (gameTime.TotalGameTime.TotalSeconds < 3)
                spriteBatch.DrawString(font_navigationKeysHelper, "Press [Left] [Right] [Up] [Down] [Shift] [Alt+Enter] [PrintSc] for navigation.", new Vector2(20, 10), Color.White);


#if SIMULATOR_DEBUG_INFO
            StringBuilder sb = new StringBuilder();
            sb.Append("FPS: ").Append(frameRate);
            spriteBatch.DrawString(font_DebugValues, sb.ToString(), new Vector2(RenderWidth - 100, 10), Color.White);
#endif


            // Cursor [this is in front of everything else]
            mouseCursor.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                0, 0, 0, 0, // pos determined in the class
                RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution, TickCount);

            spriteBatch.End();
            //skeletonMeshRenderer.End();


            // Save screenshot if render is activated
            DoScreenshot();


            base.Draw(gameTime);
        }

        /// <summary>
        /// Draws a border
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="rectangleToDraw"></param>
        /// <param name="thicknessOfBorder"></param>
        /// <param name="borderColor"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawBorder(SpriteBatch sprite, Rectangle rectangleToDraw, int thicknessOfBorder, Color borderColor)
        {
            // Draw top line
            sprite.Draw(texture_miniMapPixel, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, rectangleToDraw.Width, thicknessOfBorder), borderColor);

            // Draw left line
            sprite.Draw(texture_miniMapPixel, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, thicknessOfBorder, rectangleToDraw.Height), borderColor);

            // Draw right line
            sprite.Draw(texture_miniMapPixel, new Rectangle((rectangleToDraw.X + rectangleToDraw.Width - thicknessOfBorder),
                                            rectangleToDraw.Y,
                                            thicknessOfBorder,
                                            rectangleToDraw.Height), borderColor);
            // Draw bottom line
            sprite.Draw(texture_miniMapPixel, new Rectangle(rectangleToDraw.X,
                                            rectangleToDraw.Y + rectangleToDraw.Height - thicknessOfBorder,
                                            rectangleToDraw.Width,
                                            thicknessOfBorder), borderColor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawLine(SpriteBatch sprite, Vector2 start, Vector2 end, Color color)
        {
            int width = (int)Vector2.Distance(start, end);
            float rotation = (float)Math.Atan2((double)(end.Y - start.Y), (double)(end.X - start.X));
            sprite.Draw(texture_miniMapPixel, new Rectangle((int)start.X, (int)start.Y, width, UserSettings.LineWidth), null, color, rotation, new Vector2(0f, 0f), SpriteEffects.None, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillRectangle(SpriteBatch sprite, Rectangle rectangle, Color color)
        {
            sprite.Draw(texture_miniMapPixel, rectangle, color);
        }
        #endregion

        #region Screenshot
        /// <summary>
        /// Creates a snapshot of the current Graphics Device back buffer data 
        /// and save as JPG in the local folder
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoScreenshot()
        {
            if (bSaveScreenshot)
            {
                bSaveScreenshot = false;

                //Pull the picture from the buffer 
                int[] backBuffer = new int[RenderWidth * RenderHeight];
                GraphicsDevice.GetBackBufferData(backBuffer);

                //Copy to texture
                using (Texture2D texture = new Texture2D(GraphicsDevice, RenderWidth, RenderHeight, false, SurfaceFormat.Color))
                {
                    texture.SetData(backBuffer);

                    //Get a date for file name
                    DateTime dateTimeNow = DateTime.Now;
                    string fileName = String.Format("Maple_{0}{1}{2}_{3}{4}{5}.png",
                            dateTimeNow.Day.ToString("D2"), dateTimeNow.Month.ToString("D2"), (dateTimeNow.Year - 2000).ToString("D2"),
                             dateTimeNow.Hour.ToString("D2"), dateTimeNow.Minute.ToString("D2"), dateTimeNow.Second.ToString("D2")
                            );

                    using (MemoryStream stream_png = new MemoryStream()) // memorystream for png
                    {
                        texture.SaveAsPng(stream_png, RenderWidth, RenderHeight); // save to png stream

                        System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(stream_png);
                        ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);

                        // Create an EncoderParameters object.
                        // An EncoderParameters object has an array of EncoderParameter
                        // objects. 
                        EncoderParameters myEncoderParameters = new EncoderParameters(1);
                        myEncoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L); // max quality

                        bitmap.Save(fileName, jpgEncoder, myEncoderParameters);

                        /*using (MemoryStream stream_jpeg = new MemoryStream()) // memorystream for jpeg
                        {
                            var imageStream_png = System.Drawing.Image.FromStream(stream_png, true); // png Image
                            imageStream_png.Save(stream_jpeg, ImageFormat.Jpeg); // save as jpeg  - TODO: Fix System.Runtime.InteropServices.ExternalException: 'A generic error occurred in GDI+.' sometimes.. no idea

                            byte[] jpegOutStream = stream_jpeg.ToArray();

                            // Save
                            using (FileStream fs = File.Open(fileName, FileMode.OpenOrCreate))
                            {
                                fs.Write(jpegOutStream, 0, jpegOutStream.Length);
                            }
                        }*/
                    }
                }
                bSaveScreenshotComplete = true;
            }
        }
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {

            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        #endregion

        #region Boundaries
        /// <summary>
        /// Move the camera X viewing range by a specific offset, & centering if needed.
        /// </summary>
        /// <param name="bIsLeftKeyPressed"></param>
        /// <param name="bIsRightKeyPressed"></param>
        /// <param name="moveOffset"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCameraMoveX(bool bIsLeftKeyPressed, bool bIsRightKeyPressed, int moveOffset)
        {
            int leftRightVRDifference = (int)((vr_fieldBoundary.Right - vr_fieldBoundary.Left) * RenderObjectScaling);
            if (leftRightVRDifference < RenderWidth) // viewing range is smaller than the render width.. keep the rendering position at the center instead (starts from left to right)
            {
                /*
                 * Orbis Tower <20th Floor>
                 *  |____________|
                 *  |____________|
                 *  |____________|
                 *  |____________|
                 *  |____________|
                 *  |____________|
                 *  |____________|
                 *  |____________|
                 *  
                 * vr.Left = 87
                 * vr.Right = 827
                 * Difference = 740px
                 * vr.Center = ((vr.Right - vr.Left) / 2) + vr.Left
                 * 
                 * Viewing Width = 1024 
                 * Relative viewing center = vr.Center - (Viewing Width / 2)
                 */
                mapShiftX = ((leftRightVRDifference / 2) + (int)(vr_fieldBoundary.Left * RenderObjectScaling)) - (RenderWidth / 2);
            }
            else
            {
                // System.Diagnostics.Debug.WriteLine("[{4}] VR.Right {0}, Width {1}, Relative {2}. [Scaling {3}]", 
                //      vr.Right, RenderWidth, (int)(vr.Right - RenderWidth), (int)((vr.Right - (RenderWidth * RenderObjectScaling)) * RenderObjectScaling),
                //     mapShiftX + offset);

                if (bIsLeftKeyPressed)
                    mapShiftX =
                        Math.Max(
                            (int)(vr_fieldBoundary.Left * RenderObjectScaling),
                            mapShiftX - moveOffset);

                else if (bIsRightKeyPressed)
                    mapShiftX =
                        Math.Min(
                             (int)((vr_fieldBoundary.Right - (RenderWidth / RenderObjectScaling))),
                            mapShiftX + moveOffset);
            }
        }

        /// <summary>
        /// Move the camera Y viewing range by a specific offset, & centering if needed.
        /// </summary>
        /// <param name="bIsUpKeyPressed"></param>
        /// <param name="bIsDownKeyPressed"></param>
        /// <param name="moveOffset"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCameraMoveY(bool bIsUpKeyPressed, bool bIsDownKeyPressed, int moveOffset)
        {
            int topDownVRDifference = (int)((vr_fieldBoundary.Bottom - vr_fieldBoundary.Top) * RenderObjectScaling);
            if (topDownVRDifference < RenderHeight)
            {
                mapShiftY = ((topDownVRDifference / 2) + (int)(vr_fieldBoundary.Top * RenderObjectScaling)) - (RenderHeight / 2);
            }
            else
            {
                /*System.Diagnostics.Debug.WriteLine("[{0}] VR.Bottom {1}, Height {2}, Relative {3}. [Scaling {4}]",
                    (int)((vr.Bottom - (RenderHeight))),
                    vr.Bottom, RenderHeight, (int)(vr.Bottom - RenderHeight),
                    mapShiftX + offset);*/


                if (bIsUpKeyPressed)
                    mapShiftY =
                        Math.Max(
                            (int)(vr_fieldBoundary.Top),
                            mapShiftY - moveOffset);

                else if (bIsDownKeyPressed)
                    mapShiftY =
                        Math.Min(
                            (int)((vr_fieldBoundary.Bottom - (RenderHeight / RenderObjectScaling))),
                            mapShiftY + moveOffset);
            }
        }
        #endregion

        #region Spine specific
        public void Start(AnimationState state, int trackIndex)
        {
#if !WINDOWS_STOREAPP
            Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": start");
#endif
        }

        public void End(AnimationState state, int trackIndex)
        {
#if !WINDOWS_STOREAPP
            Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": end");
#endif
        }

        public void Complete(AnimationState state, int trackIndex, int loopCount)
        {
#if !WINDOWS_STOREAPP
            Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": complete " + loopCount);
#endif
        }

        public void Event(AnimationState state, int trackIndex, Event e)
        {
#if !WINDOWS_STOREAPP
            Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": event " + e);
#endif
        }
        #endregion
    }
}
