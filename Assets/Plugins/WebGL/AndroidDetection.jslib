mergeInto(LibraryManager.library, {
    IsAndroid: function () {
        if (typeof window.isAndroid !== 'undefined') {
            return window.isAndroid;
        }
        return /Android/i.test(navigator.userAgent);
    },
    
    GetNavigationBarHeight: function () {
        // Try to get actual navigation bar height
        // In WebGL, this is tricky - safe area is better
        return 0;
    }
});
