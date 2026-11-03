// The audio session of the page (Safari 16.4+): with the type "auto" iOS and iPadOS treat WebAudio as ambient sound and the
// silent mode of the device mutes it - the game plays, nothing is heard. "playback" makes it media, heard in the silent mode
// as a video is. Set before createUnityInstance, so before Unity makes its AudioContext.
if (navigator.audioSession) navigator.audioSession.type = "playback";
