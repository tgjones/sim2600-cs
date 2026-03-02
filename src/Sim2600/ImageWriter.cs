using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Sim2600;

public sealed class ImageWriter
{
    // For HBlank and visible image
    private const int ScanlineNumPixels = 228;

    // 3 lines vsync, 37 lines vblank,
    // 192 lines of image, 30 lines overscan
    private const int FrameHeightPixels = 262;

    private readonly string _fileNamePrefix;
    private int _imageWidth = ScanlineNumPixels;
    private int _imageHeight = FrameHeightPixels;
    private int _lastPixelX, _lastPixelY;
    private int _frameCount;

    private Image<Rgba32> _image;

    public ImageWriter(string fileNamePrefix)
    {
        _fileNamePrefix = fileNamePrefix;
        _image = new Image<Rgba32>(_imageWidth, _imageHeight, new Rgba32(0xFF, 0xFF, 0xFF, 0xFF));
    }

    public void SetNextPixel(Rgba rgba)
    {
        SetPixel(_lastPixelX, _lastPixelY, rgba);
        _lastPixelX++;
        if (_lastPixelX >= _imageWidth)
        {
            StartNextScanline();
        }
    }

    private void SetPixel(int x, int y, Rgba rgba)
    {
        _image[x, y] = new Rgba32(rgba.R, rgba.G, rgba.B, rgba.A);
    }

    private void StartNextScanline()
    {
        _lastPixelX = 0;
        _lastPixelY++;
        if (_lastPixelY >= _imageHeight)
        {
            _lastPixelY = 0;
        }
    }

    public bool RestartImage()
    {
        // Save if we've got more than 80% of a frame
        var result = false;
        if (_lastPixelY >= FrameHeightPixels * 0.8)
        {
            var fileName = $"{_fileNamePrefix}-{_frameCount}.png";
            Console.WriteLine($"Saving frame {_frameCount} to {fileName}");
            _image.SaveAsPng(fileName);
            _frameCount++;
            result = true;
        }
        _lastPixelX = 0;
        _lastPixelY = 0;
        return result;
    }
}
