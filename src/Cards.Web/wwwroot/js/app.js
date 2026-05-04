window.cardsApp = {
    _pasteHandler: null,

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
    }
};
