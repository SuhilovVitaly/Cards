window.cardsApp = {
    _pasteHandler: null,

    initPasteHandlers: function (dotNetRef) {
        let activeSlot = 1;

        const el1 = document.getElementById('text1');
        const el2 = document.getElementById('text2');

        if (el1) el1.addEventListener('focus', () => activeSlot = 1);
        if (el2) el2.addEventListener('focus', () => activeSlot = 2);

        window.cardsApp._pasteHandler = async (e) => {
            const items = e.clipboardData?.items;
            if (!items) return;

            for (const item of items) {
                if (item.type.startsWith('image/')) {
                    e.preventDefault();
                    const blob = item.getAsFile();
                    const reader = new FileReader();
                    reader.onload = () => {
                        dotNetRef.invokeMethodAsync('OnImagePasted', activeSlot, reader.result);
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
