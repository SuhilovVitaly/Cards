window.cardsApp = {
    _pasteHandler: null,
    _mediaRecorder: null,
    _mediaStream: null,
    _recordChunks: [],
    _recordTimeoutId: null,
    _recordDotNetRef: null,
    _recordCompleteMethod: null,
    _recordErrorMethod: null,
    _recordCompleted: false,
    _audioElement: null,

    initPasteHandlers: function (dotNetRef) {
        window.cardsApp._pasteHandler = async (e) => {
            const items = e.clipboardData?.items;
            if (!items) return;

            for (const item of items) {
                if (item.type.startsWith('image/')) {
                    e.preventDefault();
                    const blob = item.getAsFile();
                    const reader = new FileReader();
                    reader.onload = () => {
                        dotNetRef.invokeMethodAsync('OnImagePasted', reader.result);
                    };
                    reader.readAsDataURL(blob);
                    return;
                }
            }
        };

        document.addEventListener('paste', window.cardsApp._pasteHandler);
    },

    disposePasteHandlers: function () {
        if (window.cardsApp._pasteHandler) {
            document.removeEventListener('paste', window.cardsApp._pasteHandler);
            window.cardsApp._pasteHandler = null;
        }
    },

    focusElement: function (elementId) {
        setTimeout(() => {
            const el = document.getElementById(elementId);
            if (el) el.focus();
        }, 50);
    },

    speakText: function (text, langCode) {
        if (!window.speechSynthesis || !text) return;
        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = langCode;
        utterance.rate = 0.9;
        window.speechSynthesis.speak(utterance);
    },

    stopSpeaking: function () {
        if (window.speechSynthesis) {
            window.speechSynthesis.cancel();
        }
    },

    startRecording: async function (dotNetRef, onCompleteMethod, maxSeconds, onErrorMethod) {
        const app = window.cardsApp;
        if (app._mediaRecorder) {
            app.stopRecording();
        }

        app._recordDotNetRef = dotNetRef;
        app._recordCompleteMethod = onCompleteMethod;
        app._recordErrorMethod = onErrorMethod || null;
        app._recordChunks = [];
        app._recordCompleted = false;

        const reportError = (message) => {
            if (app._recordDotNetRef && app._recordErrorMethod) {
                app._recordDotNetRef.invokeMethodAsync(app._recordErrorMethod, message);
            }
        };

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            reportError('Microphone unavailable');
            return false;
        }

        let stream;
        try {
            stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        } catch (err) {
            reportError(err && err.message ? err.message : 'Microphone access denied');
            return false;
        }

        app._mediaStream = stream;

        let recorder;
        try {
            const options = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
                ? { mimeType: 'audio/webm;codecs=opus' }
                : (MediaRecorder.isTypeSupported('audio/webm') ? { mimeType: 'audio/webm' } : undefined);
            recorder = options ? new MediaRecorder(stream, options) : new MediaRecorder(stream);
        } catch (err) {
            stream.getTracks().forEach(t => t.stop());
            app._mediaStream = null;
            reportError(err && err.message ? err.message : 'Recorder unavailable');
            return false;
        }

        app._mediaRecorder = recorder;

        recorder.ondataavailable = (e) => {
            if (e.data && e.data.size > 0) app._recordChunks.push(e.data);
        };

        recorder.onstop = () => {
            const chunks = app._recordChunks;
            const ref = app._recordDotNetRef;
            const method = app._recordCompleteMethod;

            if (app._recordTimeoutId) {
                clearTimeout(app._recordTimeoutId);
                app._recordTimeoutId = null;
            }
            if (app._mediaStream) {
                app._mediaStream.getTracks().forEach(t => t.stop());
                app._mediaStream = null;
            }
            app._mediaRecorder = null;
            app._recordChunks = [];
            app._recordCompleted = true;

            const type = (recorder.mimeType && recorder.mimeType.length > 0) ? recorder.mimeType : 'audio/webm';
            const blob = new Blob(chunks, { type });

            if (blob.size === 0) {
                if (ref && method) ref.invokeMethodAsync(method, null);
                return;
            }

            const reader = new FileReader();
            reader.onloadend = () => {
                if (ref && method) ref.invokeMethodAsync(method, reader.result);
            };
            reader.onerror = () => {
                if (ref && method) ref.invokeMethodAsync(method, null);
            };
            reader.readAsDataURL(blob);
        };

        recorder.onerror = (e) => {
            reportError((e && e.error && e.error.message) ? e.error.message : 'Recording error');
        };

        recorder.start();

        const seconds = (typeof maxSeconds === 'number' && maxSeconds > 0) ? maxSeconds : 10;
        app._recordTimeoutId = setTimeout(() => {
            app.stopRecording();
        }, seconds * 1000);

        return true;
    },

    stopRecording: function () {
        const app = window.cardsApp;
        if (app._recordTimeoutId) {
            clearTimeout(app._recordTimeoutId);
            app._recordTimeoutId = null;
        }
        const recorder = app._mediaRecorder;
        if (recorder && recorder.state !== 'inactive') {
            try { recorder.stop(); } catch (_) { /* ignore */ }
        } else if (app._mediaStream) {
            app._mediaStream.getTracks().forEach(t => t.stop());
            app._mediaStream = null;
            app._mediaRecorder = null;
        }
    },

    playRecording: function (src) {
        if (!src) return;
        if (window.speechSynthesis) {
            window.speechSynthesis.cancel();
        }
        const app = window.cardsApp;
        if (app._audioElement) {
            try { app._audioElement.pause(); } catch (_) { /* ignore */ }
            app._audioElement = null;
        }
        const audio = new Audio(src);
        app._audioElement = audio;
        audio.play().catch(() => { /* ignore playback errors */ });
    }
};
