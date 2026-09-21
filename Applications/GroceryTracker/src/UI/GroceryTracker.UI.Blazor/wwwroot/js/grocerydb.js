// IndexedDB backing store for the offline-first cache and the change outbox.
//
// Everything the UI reads comes from here, never straight from the network, so the app behaves the
// same in the shop with no signal as it does at home. The `outbox` store holds changes that have
// not reached the API yet; `meta` holds the sync cursor.

const DB_NAME = 'grocerytracker';
// Version 2 adds the settlements store. Upgrading only creates what is missing, so an existing
// device keeps its cache and its outbox.
const DB_VERSION = 2;

export const ENTITY_STORES = ['households', 'members', 'stores', 'categories', 'trips', 'items', 'settlements'];

let dbPromise = null;

function openDatabase() {
  if (dbPromise) {
    return dbPromise;
  }

  dbPromise = new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = event => {
      const db = event.target.result;

      for (const name of ENTITY_STORES) {
        if (!db.objectStoreNames.contains(name)) {
          db.createObjectStore(name, { keyPath: 'id' });
        }
      }

      if (!db.objectStoreNames.contains('outbox')) {
        // autoIncrement keeps the queue in the order the edits were made, which is the order the
        // server must replay them in.
        db.createObjectStore('outbox', { keyPath: 'sequence', autoIncrement: true });
      }

      if (!db.objectStoreNames.contains('meta')) {
        db.createObjectStore('meta', { keyPath: 'key' });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
    request.onblocked = () => reject(new Error('IndexedDB upgrade blocked by another open tab.'));
  });

  return dbPromise;
}

function runTransaction(storeNames, mode, work) {
  return openDatabase().then(db => new Promise((resolve, reject) => {
    const tx = db.transaction(storeNames, mode);
    let result;

    tx.oncomplete = () => resolve(result);
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error ?? new Error('IndexedDB transaction aborted.'));

    try {
      result = work(tx);
    } catch (error) {
      tx.abort();
      reject(error);
    }
  }));
}

export async function getAll(storeName) {
  const db = await openDatabase();
  return new Promise((resolve, reject) => {
    const req = db.transaction(storeName, 'readonly').objectStore(storeName).getAll();
    req.onsuccess = () => resolve(req.result ?? []);
    req.onerror = () => reject(req.error);
  });
}

export async function get(storeName, id) {
  const db = await openDatabase();
  return new Promise((resolve, reject) => {
    const req = db.transaction(storeName, 'readonly').objectStore(storeName).get(id);
    req.onsuccess = () => resolve(req.result ?? null);
    req.onerror = () => reject(req.error);
  });
}

export async function put(storeName, value) {
  await runTransaction(storeName, 'readwrite', tx => tx.objectStore(storeName).put(value));
  return true;
}

export async function putMany(storeName, values) {
  if (!values || values.length === 0) {
    return true;
  }

  await runTransaction(storeName, 'readwrite', tx => {
    const store = tx.objectStore(storeName);
    for (const value of values) {
      store.put(value);
    }
  });

  return true;
}

/// Applies a whole server payload in one transaction so the cache never sits in a half-updated
/// state if the tab is closed mid-write.
export async function applyPayload(payload) {
  const groups = Object.entries(payload).filter(([name]) => ENTITY_STORES.includes(name));
  const touched = groups.filter(([, values]) => values && values.length > 0);

  if (touched.length === 0) {
    return true;
  }

  await runTransaction(touched.map(([name]) => name), 'readwrite', tx => {
    for (const [name, values] of touched) {
      const store = tx.objectStore(name);
      for (const value of values) {
        store.put(value);
      }
    }
  });

  return true;
}

export async function enqueue(operation) {
  await runTransaction('outbox', 'readwrite', tx => tx.objectStore('outbox').add(operation));
  return true;
}

export async function getOutbox() {
  const db = await openDatabase();
  return new Promise((resolve, reject) => {
    const req = db.transaction('outbox', 'readonly').objectStore('outbox').getAll();
    req.onsuccess = () => resolve(req.result ?? []);
    req.onerror = () => reject(req.error);
  });
}

export async function removeOperations(operationIds) {
  if (!operationIds || operationIds.length === 0) {
    return true;
  }

  const wanted = new Set(operationIds);

  await runTransaction('outbox', 'readwrite', tx => {
    const store = tx.objectStore('outbox');
    const cursorRequest = store.openCursor();

    cursorRequest.onsuccess = () => {
      const cursor = cursorRequest.result;
      if (!cursor) {
        return;
      }

      if (wanted.has(cursor.value.operationId)) {
        cursor.delete();
      }

      cursor.continue();
    };
  });

  return true;
}

export async function getMeta(key) {
  const db = await openDatabase();
  return new Promise((resolve, reject) => {
    const req = db.transaction('meta', 'readonly').objectStore('meta').get(key);
    req.onsuccess = () => resolve(req.result ? req.result.value : null);
    req.onerror = () => reject(req.error);
  });
}

export async function setMeta(key, value) {
  await runTransaction('meta', 'readwrite', tx => tx.objectStore('meta').put({ key, value }));
  return true;
}

/// Wipes the cache and the queue. Used by the "reset local data" action when a device's copy has
/// drifted badly enough that a clean re-pull is the quickest fix.
export async function clearAll() {
  const names = [...ENTITY_STORES, 'outbox', 'meta'];
  await runTransaction(names, 'readwrite', tx => {
    for (const name of names) {
      tx.objectStore(name).clear();
    }
  });

  return true;
}
