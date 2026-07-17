# Model files (not included — download these)

This folder must contain two ONNX model files from the OpenCV Zoo before the
app will run. They are binary files and licensed separately from this code, so
they are not bundled here.

1. **Face detector — YuNet**
   File name expected by config: `yunet.onnx`
   Source: OpenCV Zoo, `models/face_detection_yunet` folder
   (search "opencv zoo face_detection_yunet" — get the `.onnx` file, e.g.
   `face_detection_yunet_2023mar.onnx`, and rename/reference it as configured
   in `appsettings.json`).

2. **Face recognizer — SFace**
   File name expected by config: `face_recognition_sface.onnx`
   Source: OpenCV Zoo, `models/face_recognition_sface` folder
   (e.g. `face_recognition_sface_2021dec.onnx`).

Both models are small (a few MB total) and are the same pair used in OpenCV's
own official face-detection-and-recognition sample, so any version pulled from
the OpenCV Zoo repository should work with `FaceDetectorYN`/`FaceRecognizerSF`.

Update the paths in `src/MultiFaceRec.UI/appsettings.json` if you place the
files somewhere other than this `models/` folder.
