window.obwcTheme = {
    set: (dark) => document.documentElement.classList.toggle("obwc-dark", !!dark)
};

window.obwcViewport = {
    wide: () => window.matchMedia("(min-width: 960px)").matches
};

window.obwcStorage = {
    get: (key) => localStorage.getItem(key),
    set: (key, value) => localStorage.setItem(key, value),
    remove: (key) => localStorage.removeItem(key)
};

window.obwcScroll = {
    toId: (id) => {
        const element = document.getElementById(id);
        if (!element)
            return false;
        element.scrollIntoView({ behavior: "smooth", block: "start" });
        return true;
    },
    fragment: () => {
        const hash = window.location.hash;
        if (!hash || hash.length < 2)
            return "";
        try { return decodeURIComponent(hash.substring(1)); }
        catch { return hash.substring(1); }
    }
};

window.obwcDownload = (fileName, bytes) => {
    const blob = new Blob([new Uint8Array(bytes)]);
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName || "download";
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
};
