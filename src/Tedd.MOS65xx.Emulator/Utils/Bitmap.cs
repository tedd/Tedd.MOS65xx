//using System;
//using System.Drawing;
//using System.Drawing.Imaging;
//using System.IO;
//using System.Runtime.InteropServices;
//using System.Windows.Interop;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;

//namespace Tedd.MOS65xx.Emulator.Utils
//{
//    using PixelFormat = System.Windows.Media.PixelFormat;

//    /// <summary>
//    /// This object holds a byte array of the picture as well as a BitmapSource for WPF objects to bind to. Simply call .Invalidate() to update GUI.
//    /// </summary>
//    public unsafe class Bitmap : IDisposable
//    {
//        // some ideas/code borowed from CL NUI sample CLNUIImage.cs
//        [DllImport("kernel32.dll", SetLastError = true)]
//        private static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);
//        [DllImport("kernel32.dll", SetLastError = true)]
//        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, uint dwNumberOfBytesToMap);
//        [DllImport("kernel32.dll", SetLastError = true)]
//        private static extern bool UnmapViewOfFile(IntPtr hMap);
//        [DllImport("kernel32.dll", SetLastError = true)]
//        private static extern bool CloseHandle(IntPtr hHandle);

//        //[DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
//        //private static extern void CopyMemory(IntPtr Destination, IntPtr Source, uint Length);

//        private IntPtr _section = IntPtr.Zero;
//        public IntPtr ImageData { get; private set; }
//        public InteropBitmap BitmapSource { get; private set; }
//        public int BytesPerPixel { get; private set; }
//        public uint ByteLength { get; private set; }
//        public int Height { get; private set; }
//        public int Width { get; private set; }
//        public PixelFormat PixelFormat { get; private set; }
//        public readonly object LockObject = new object();

//        public unsafe uint* ImageUIntPtr { get; private set; }
//        public unsafe int* ImageIntPtr { get; private set; }
//        public unsafe byte* ImageBytePtr { get; private set; }

//        /// <summary>
//        /// Initializes an empty Bitmap
//        /// </summary>
//        /// <param name="width">Image width</param>
//        /// <param name="height">Image height</param>
//        /// <param name="pixelFormat">Image format</param>
//        public Bitmap(int width, int height, PixelFormat pixelFormat)
//        {
//            PixelFormat = pixelFormat;
//            BytesPerPixel = pixelFormat.BitsPerPixel / 8;
//            uint imageSize = (uint)width * (uint)height * (uint)BytesPerPixel;
//            ByteLength = imageSize;
//            // create memory section and map
//            _section = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, imageSize, null);
//            ImageData = MapViewOfFile(_section, 0xF001F, 0, 0, imageSize);
//            BitmapSource = Imaging.CreateBitmapSourceFromMemorySection(_section, width, height, pixelFormat, width * BytesPerPixel, 0) as InteropBitmap;
//            ImageIntPtr = (int*)ImageData;
//            ImageUIntPtr = (uint*)ImageData;
//            ImageBytePtr = (byte*)ImageData;

//            Height = height;
//            Width = width;
//        }

//        /// <summary>
//        /// Initializes an Bitmap from a System.Drawing.Bitmap
//        /// </summary>
//        /// <param name="width">Image width</param>
//        /// <param name="height">Image height</param>
//        /// <param name="pixelFormat">Image format</param>
//        public Bitmap(int width, int height, System.Drawing.Bitmap bitmap, PixelFormat pixelFormat)
//        {

//            PixelFormat = pixelFormat;
//            BytesPerPixel = pixelFormat.BitsPerPixel / 8;
//            uint imageSize = (uint)width * (uint)height * (uint)BytesPerPixel;

//            ByteLength = imageSize;
//            // create memory section and map
//            _section = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, imageSize, null);
//            ImageData = MapViewOfFile(_section, 0xF001F, 0, 0, imageSize);
//            BitmapSource = Imaging.CreateBitmapSourceFromMemorySection(_section, width, height, pixelFormat, width * BytesPerPixel, 0) as InteropBitmap;
//            ImageIntPtr = (int*)ImageData;
//            ImageBytePtr = (byte*)ImageData;
//            Height = height;
//            Width = width;

//            CloneFromBitmap(bitmap);
//        }
//        /// <summary>
//        /// Initializes an Bitmap from a System.Drawing.Bitmap
//        /// </summary>
//        /// <param name="width">Image width</param>
//        /// <param name="height">Image height</param>
//        /// <param name="pixelFormat">Image format</param>
//        public Bitmap(System.Drawing.Bitmap bitmap, PixelFormat pixelFormat)
//        {

//            int width = bitmap.Width;
//            int height = bitmap.Height;
//            PixelFormat = pixelFormat;
//            BytesPerPixel = pixelFormat.BitsPerPixel / 8;
//            uint imageSize = (uint)width * (uint)height * (uint)BytesPerPixel;

//            ByteLength = imageSize;
//            // create memory section and map
//            _section = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, imageSize, null);
//            ImageData = MapViewOfFile(_section, 0xF001F, 0, 0, imageSize);
//            BitmapSource = Imaging.CreateBitmapSourceFromMemorySection(_section, width, height, pixelFormat, width * BytesPerPixel, 0) as InteropBitmap;
//            ImageIntPtr = (int*)ImageData;
//            ImageBytePtr = (byte*)ImageData;
//            Height = height;
//            Width = width;

//            CloneFromBitmap(bitmap);
//        }
//        // Discarded - not fast enough so should not be used.
//        //[System.Runtime.CompilerServices.IndexerName("TheItem")]
//        //public int this[int index]   // Indexer declaration
//        //{
//        //}
//        //[System.Runtime.CompilerServices.IndexerName("TheItem")]
//        //public int this[int x, int y]   // Indexer declaration
//        //{
//        //    get
//        //    {
//        //        int p = (int) ((x*this.BitmapSource.Width*this.BytesPerPixel) + (y*this.BytesPerPixel));
//        //        return ImageData[p];
//        //    }
//        //}


//        /// <summary>
//        /// Invalidates the bitmap causing a redraw
//        /// </summary>
//        public void Invalidate()
//        {
//            //lock (LockObject)
//            //{
//            BitmapSource.Invalidate();
//            //}
//        }
//        ~Bitmap()
//        {
//            Dispose();
//        }
//        private bool _disposed;

//        public void Dispose()
//        {
//            Dispose(true);
//            GC.SuppressFinalize(this);
//        }



//        protected void Dispose(bool disposing)
//        {
//            lock (LockObject)
//            {
//                if (disposing)
//                {
//                    // free managed resources
//                }
//                // free native resources if there are any.
//                if (ImageData != IntPtr.Zero)
//                {
//                    UnmapViewOfFile(ImageData);
//                    ImageData = IntPtr.Zero;
//                }
//                if (_section != IntPtr.Zero)
//                {
//                    CloseHandle(_section);
//                    _section = IntPtr.Zero;
//                }
//            }
//        }



//        public unsafe void CopyTo(Bitmap destinationBitmap, int top, int left, int height, int width)
//        {
//            lock (LockObject)
//            {
//                // Find smallest array
//                //int size = (int)Math.Min(ByteLength, destinationBitmap.ByteLength) / destinationBitmap.BytesPerPixel;
//                // Copy memory
//                //CopyMemory(ImageData, destinationBitmap.ImageData, size);
//                //Marshal.Copy(src, destinationBitmap.ImageData, 0, size);

//                // First copy int64 as far as we can (faster than copying single bytes)
//                Int64* src64 = (Int64*)ImageData;
//                Int64* dst64 = (Int64*)destinationBitmap.ImageData;
//                byte* src8 = (byte*)ImageData;
//                byte* dst8 = (byte*)destinationBitmap.ImageData;

//                int maxWidth = Math.Min(Math.Min(Width - left, destinationBitmap.Width - left), width + top);
//                int maxHeight = Math.Min(Math.Min(Height - top, destinationBitmap.Height - top), height + top);

//                for (int x = top; x < maxHeight; x++)
//                {
//                    for (int y = left; y < maxWidth; y++)
//                    {
//                        int srcp = (x * Width) + y;
//                        int dstp = (x * destinationBitmap.Width) + y;
//                        dst8[dstp] = src8[srcp];
//                    }
//                }

//            }

//        }
//        public unsafe void CopyTo(Bitmap destinationBitmap)
//        {
//            lock (LockObject)
//            {
//                // Find smallest array
//                //int size = (int)Math.Min(ByteLength, destinationBitmap.ByteLength) / destinationBitmap.BytesPerPixel;
//                // Copy memory
//                //CopyMemory(ImageData, destinationBitmap.ImageData, size);
//                //Marshal.Copy(src, destinationBitmap.ImageData, 0, size);

//                // First copy int64 as far as we can (faster than copying single bytes)
//                int copied = 0;
//                Int64* src64 = (Int64*)ImageData;
//                Int64* dst64 = (Int64*)destinationBitmap.ImageData;

//                int srcHeight = Height * Width;
//                int dstHeight = destinationBitmap.Height * destinationBitmap.Width;
//                int maxLen = Math.Min(srcHeight, dstHeight);

//                int copyLength = maxLen - 8;
//                int i = 0;
//                while (copied < copyLength)
//                {
//                    dst64[i] = src64[i];
//                    i++;
//                    copied += 8;
//                }

//                // Then copy single bytes until end of data
//                byte* src8 = (byte*)ImageData;
//                byte* dst8 = (byte*)destinationBitmap.ImageData;

//                i *= 8;
//                while (copied < ByteLength)
//                {
//                    dst8[i] = src8[i];
//                    i++;
//                    copied++;
//                }

//            }
//        }

//        public unsafe void CloneFromBitmap(System.Drawing.Bitmap sourceBitmap)
//        {
//            throw new NotImplementedException("Untested!");
//            lock (LockObject)
//            {

//                // First copy int64 as far as we can (faster than copying single bytes)
//                int copied = 0;
//                BitmapData bData = sourceBitmap.LockBits(new Rectangle(new Point(), sourceBitmap.Size), ImageLockMode.ReadOnly, sourceBitmap.PixelFormat);
//                Int64* src64 = (Int64*)bData.Scan0.ToPointer();
//                Int64* dst64 = (Int64*)ImageData;

//                int srcHeight = sourceBitmap.Height * sourceBitmap.Width;
//                int dstHeight = Height * Width;
//                int maxLen = Math.Min(srcHeight, dstHeight);

//                int copyLength = maxLen - 8;
//                int i = 0;
//                while (copied < copyLength)
//                {
//                    dst64[i] = src64[i];
//                    i++;
//                    copied += 8;
//                }

//                // Then copy single bytes until end of data
//                byte* src8 = (byte*)bData.Scan0.ToPointer();
//                byte* dst8 = (byte*)ImageData;

//                i *= 8;
//                while (copied < ByteLength)
//                {
//                    dst8[i] = src8[i];
//                    i++;
//                    copied++;
//                }

//            }
//        }



//        public Bitmap Clone()
//        {
//            lock (LockObject)
//            {
//                // Create new bitmap
//                Bitmap bitmap = new Bitmap(Width, Height, PixelFormat);
//                // Copy data into new bitmap
//                CopyTo(bitmap);

//                return bitmap;
//            }
//        }

//        public static Bitmap CreateFromFile(string file)
//        {
//            using (Image image = Image.FromFile(file))
//            {
//                using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(image))
//                {

//                    System.Drawing.Imaging.PixelFormat format = bmp.PixelFormat;
//                    Bitmap ret = new Bitmap(image.Width, image.Height, PixelFormats.Bgr32);

//                    BitmapData data = bmp.LockBits(new Rectangle(0, 0, image.Width, image.Height),
//                                                   ImageLockMode.ReadOnly,
//                                                   format);
//                    int stride = data.Stride;
//                    int offset = stride - image.Width * ret.BytesPerPixel;
//                    unsafe
//                    {
//                        byte* src = (byte*)data.Scan0.ToPointer();
//                        byte* dst = (byte*)ret.ImageData;

//                        int mp = image.Height * image.Width * ret.BytesPerPixel;
//                        for (int p = 0; p < mp; p++)
//                        {
//                            dst[p] = src[p];
//                        }
//                    }
//                    return ret;

//                }
//            }
//        }

//        public static Bitmap CreateFromStream(Stream file)
//        {
//            using (Image image = Image.FromStream(file))
//            {
//                using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(image))
//                {

//                    System.Drawing.Imaging.PixelFormat format = bmp.PixelFormat;
//                    Bitmap ret = new Bitmap(image.Width, image.Height, PixelFormats.Bgr32);
//                    BitmapData data = bmp.LockBits(new Rectangle(0, 0, image.Width, image.Height),
//                                                   ImageLockMode.ReadOnly,
//                                                   format);
//                    int stride = data.Stride;
//                    int offset = stride - image.Width * ret.BytesPerPixel;
//                    unsafe
//                    {
//                        byte* src = (byte*)data.Scan0.ToPointer();
//                        byte* dst = (byte*)ret.ImageData;

//                        int mp = image.Height * image.Width * ret.BytesPerPixel;
//                        for (int p = 0; p < mp; p++)
//                        {
//                            dst[p] = src[p];
//                        }
//                    }
//                    return ret;
//                }
//            }
//        }

//        private static byte[] imageToByteArray(System.Drawing.Image imageIn)
//        {
//            using (MemoryStream ms = new MemoryStream())
//            {
//                imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Gif);
//                return ms.ToArray();
//            }
//        }

//        public void SavePNG(string filename)
//        {
//            lock (LockObject)
//            {
//                using (var fileStream = new FileStream(filename, FileMode.Create))
//                {
//                    SavePNG(fileStream);
//                }
//            }
//        }

//        public void SavePNG(Stream stream)
//        {
//            lock (LockObject)
//            {
//                BitmapEncoder encoder = new PngBitmapEncoder();
//                encoder.Frames.Add(BitmapFrame.Create(BitmapSource));
//                encoder.Save(stream);
//            }
//        }

//        public System.Drawing.Bitmap CloneToBitmap()
//        {
//            lock (LockObject)
//            {
//                return System.Drawing.Image.FromHbitmap(ImageData);
//            }
//        }

//        public void Blur(int xBlur, int yBlur)
//        {
//            Bitmap srcImage = Clone();
//            int* srcIntPtr = srcImage.ImageIntPtr;
//            int* dstIntPtr = ImageIntPtr;

//            int xfrom = (xBlur / 2) * -1;
//            int xto = xfrom + xBlur;
//            int yfrom = (yBlur / 2) * -1;
//            int yto = yfrom + yBlur;

//            for (int x = 0; x < Height; x++)
//            {
//                for (int y = 0; y < Width; y++)
//                {
//                    // Look around
//                    long avg = 0;
//                    int count = 0;
//                    for (int xa = xfrom; xa < xto; xa++)
//                    {
//                        for (int ya = yfrom; ya < yto; ya++)
//                        {
//                            if (x + xa >= 0 && x + xa < Height
//                                && y + ya >= 0 && y + ya < Width)
//                            {
//                                int p = ((x + xa) * Width) + (y + ya);
//                                avg += srcIntPtr[p];
//                                count++;
//                            }
//                        }
//                    }
//                    int pd = (x * Width) + y;
//                    dstIntPtr[pd] = (int)(avg / count);
//                }
//            }

//            srcImage.Dispose();
//        }

//        public void SwapColor32(int oldColor, int newColor)
//        {

//            for (int x = 0; x < Height; x++)
//            {
//                for (int y = 0; y < Width; y++)
//                {
//                    int p = (x * Width) + y;
//                    if (ImageIntPtr[p] == oldColor)
//                        ImageIntPtr[p] = newColor;
//                }
//            }
//        }
//    }
//}
