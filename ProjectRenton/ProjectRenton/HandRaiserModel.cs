using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ProjectRenton
{
    class HandRaiserModel
    {

        private const string _kModelFileName = "model.onnx";
        private LearningModel _model = null;
        private LearningModelSession _session;
        private LearningModelBinding _binding;
        private List<string> _labels = new List<string>();
        private int _runCount = 0;
        private static HandRaiserModel instance;
        private HandRaiserModel()
        {

        }

        public static async Task<bool> DetectObjects(StorageFile file)
        {
            VideoFrame inputImage = await convertToVideoFrame(file);
            return await DetectObjects(inputImage);
        }
        public static async Task<bool> DetectObjects(VideoFrame file)
        {
            if (instance == null)
            {
                instance = new HandRaiserModel();
            }

            SoftwareBitmap bitmap = file.SoftwareBitmap;
            using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);

                encoder.SetSoftwareBitmap(bitmap);

                encoder.BitmapTransform.ScaledWidth = 320;
                encoder.BitmapTransform.ScaledHeight = 320;
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.NearestNeighbor;

                await encoder.FlushAsync();

                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                SoftwareBitmap newBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Rgba8, bitmap.BitmapAlphaMode);
                file = VideoFrame.CreateWithSoftwareBitmap(newBitmap);
            }
            return await instance.EvaluateFrame(file);
        }


        private async Task InitModelAsync()
        {
            if (_model != null)
            {
                return;
            }

            var model_file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///model.onnx"));
            _model = await LearningModel.LoadFromStorageFileAsync(model_file);
            var device = new LearningModelDevice(LearningModelDeviceKind.Cpu);
            _session = new LearningModelSession(_model, device);
            _binding = new LearningModelBinding(_session);
        }

        public async Task<bool> EvaluateFrame(VideoFrame inputImage)
        {
            await InitModelAsync();
            _binding.Clear();
            _binding.Bind("image_tensor", inputImage);
            var results = await _session.EvaluateAsync(_binding, "");


            TensorFloat result_detected_boxes = results.Outputs["detected_boxes"] as TensorFloat;
            var data_detected_boxes = result_detected_boxes.GetAsVectorView();
            var list_detected_boxes = data_detected_boxes.ToList();


            TensorInt64Bit result_detected_classes = results.Outputs["detected_classes"] as TensorInt64Bit;
            var data_detected_classes = result_detected_classes.GetAsVectorView();
            var list_detected_classes = data_detected_classes.ToList();

            TensorFloat result_detected_scores = results.Outputs["detected_scores"] as TensorFloat;
            var data_detected_scores = result_detected_scores.GetAsVectorView();
            var list_detected_scores = data_detected_scores.ToList();


            Dictionary<Label, List<BoundingBox>> bounding_boxes = new Dictionary<Label, List<BoundingBox>>();
            bounding_boxes[Label.Face] = new List<BoundingBox>();
            bounding_boxes[Label.Hand] = new List<BoundingBox>();
            for (int i=0; i<list_detected_boxes.Count; i+=4)
            {               
                float x1 = list_detected_boxes[i];
                float y1 = list_detected_boxes[i + 1];

                float x2 = list_detected_boxes[i + 2];
                float y2 = list_detected_boxes[i + 3];

                BoundingBox box = new BoundingBox() { topLeft = new BoundingBoxPoint() { x = x1, y = y1 }, bottomRight = new BoundingBoxPoint() { x = x2, y = y2}, label = (Label)list_detected_classes[i/4] };
                bounding_boxes[box.label].Add(box);
            }
            return isHandRaised(bounding_boxes);
        }

        private bool isHandRaised(Dictionary<Label, List<BoundingBox>> boundingBoxes)
        {
            if(boundingBoxes[Label.Face].Count == 0 || boundingBoxes[Label.Hand].Count == 0)
            {
                return false;
            }
            for(int i=0; i<boundingBoxes[Label.Hand].Count; i++)
            {
                if(boundingBoxes[Label.Hand][i].topLeft.y < boundingBoxes[Label.Face][0].topLeft.y)
                {
                    return true;
                }
            }

            return false;
        }
        private static async Task<VideoFrame> convertToVideoFrame(StorageFile file)
        {
            SoftwareBitmap softwareBitmap;
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
            {
                // Create the decoder from the stream 
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                // Get the SoftwareBitmap representation of the file in BGRA8 format
                softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            // Display the image
            SoftwareBitmapSource imageSource = new SoftwareBitmapSource();
            await imageSource.SetBitmapAsync(softwareBitmap);

            // Encapsulate the image within a VideoFrame to be bound and evaluated
            VideoFrame inputImage = VideoFrame.CreateWithSoftwareBitmap(softwareBitmap);
            return inputImage;
        }
        LearningModelDeviceKind GetDeviceKind()
        {

            return LearningModelDeviceKind.Default;
        }

        struct BoundingBoxPoint
        {
            public float x;
            public float y;
        }
        struct BoundingBox
        {
            public BoundingBoxPoint topLeft;
            public BoundingBoxPoint bottomRight;
            public Label label;
        }

        enum Label
        {
            Face=0,
            Hand=1
        }
    }
}
