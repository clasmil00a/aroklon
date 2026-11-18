'use strict';

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('api', {
  invoke: (action, payload) => ipcRenderer.invoke('app:invoke', action, payload),
  meta: () => ipcRenderer.invoke('app:meta'),
  backup: () => ipcRenderer.invoke('app:backup'),
  restore: () => ipcRenderer.invoke('app:restore'),
  print: (html, opts) => ipcRenderer.invoke('app:print', html, opts),
  preview: (html) => ipcRenderer.invoke('app:preview', html),
  openDataDir: () => ipcRenderer.invoke('app:open-data-dir'),
  checkUpdate: () => ipcRenderer.invoke('app:checkUpdate'),
  applyUpdate: () => ipcRenderer.invoke('app:applyUpdate'),
  onMenu: (cb) => {
    ipcRenderer.on('menu', (_e, action) => cb(action));
  },
});