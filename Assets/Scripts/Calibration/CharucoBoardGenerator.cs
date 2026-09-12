using System;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ArucoModule;
using OpenCVForUnity.ImgcodecsModule;

namespace Thesis.Calibration {

    // Single place that renders the physical ChArUco board image, shared by the
    // dev CharucoBoardPopup and the live CalibrationTutorialScreen so both ever
    // produce pixel-identical boards for the same BoardConfig.
    public static class CharucoBoardGenerator {

        public static byte[] GeneratePng(BoardConfig config, int outputWidth = 2100) {
            var dict = Aruco.getPredefinedDictionary(config.dictionaryId);
            var board = CharucoBoard.create(config.squaresX, config.squaresY,
                config.SquareLengthM, config.MarkerLengthM, dict);

            int outputHeight = (int)Math.Round(outputWidth * config.squaresY / (float)config.squaresX);
            var img = new Mat();
            board.draw(new Size(outputWidth, outputHeight), img, 20, 1);

            var buf = new MatOfByte();
            Imgcodecs.imencode(".png", img, buf);
            byte[] bytes = buf.toArray();

            buf.Dispose();
            img.Dispose();
            board.Dispose();
            dict.Dispose();

            return bytes;
        }
    }
}
