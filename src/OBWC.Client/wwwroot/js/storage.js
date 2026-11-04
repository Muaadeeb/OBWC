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
