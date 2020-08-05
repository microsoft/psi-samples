# Speech Sample

This sample demonstrates how to build a simple speech recognition application using a number of different audio and speech components. In addition, it also demonstrates data logging and replay of logged data. The sample builds and runs on Windows.

__NOTES:__

* When disk logging (option 4) is turned on, audio and speech recognition stream data will be logged to folders on your hard drive. By default, the root location where log folders will be created is `.\OutputData` (relative to the working directory of the running sample). You may change this by setting the static `LogPath` variable in the sample code.
* Playback from a logged session (option 3) requires that at least one logged session be previously saved to disk (option 4).

## Additional Prerequisites

* In order to run the AzureSpeechRecognizer portion of this sample (option 2 in the sample menu), you must have a valid Cognitive Services Speech subscription key. You may enter this key at runtime, or set it in the static `AzureSubscriptionKey` variable. For more information on how to obtain a subscription key for the Azure Speech Service, see: https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/get-started
