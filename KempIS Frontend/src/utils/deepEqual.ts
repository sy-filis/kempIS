export function equal(a: unknown, b: unknown): boolean {
  if (a === b) {
    return true;
  }

  if (!a || !b || typeof a !== "object" || typeof b !== "object") {
    return a !== a && b !== b;
  }

  const aObj = a as Record<string, unknown> & {
    constructor: unknown;
    valueOf: () => unknown;
    toString: () => string;
  };
  const bObj = b as Record<string, unknown> & {
    constructor: unknown;
    valueOf: () => unknown;
    toString: () => string;
  };

  if (aObj.constructor !== bObj.constructor) {
    return false;
  }

  if (Array.isArray(a)) {
    const bArr = b as unknown[];
    const length = a.length;
    if (length !== bArr.length) {
      return false;
    }
    for (let i = length; i-- !== 0; ) {
      if (!equal(a[i], bArr[i])) {
        return false;
      }
    }
    return true;
  }

  if (a instanceof Map) {
    const bMap = b as Map<unknown, unknown>;
    if (a.size !== bMap.size) {
      return false;
    }
    for (const entry of a.entries()) {
      if (!bMap.has(entry[0])) {
        return false;
      }
    }
    for (const entry of a.entries()) {
      if (!equal(entry[1], bMap.get(entry[0]))) {
        return false;
      }
    }
    return true;
  }

  if (a instanceof Set) {
    const bSet = b as Set<unknown>;
    if (a.size !== bSet.size) {
      return false;
    }
    for (const entry of a.entries()) {
      if (!bSet.has(entry[0])) {
        return false;
      }
    }
    return true;
  }

  if (ArrayBuffer.isView(a)) {
    const aView = a as ArrayBufferView & {
      length: number;
      [k: number]: unknown;
    };
    const bView = b as ArrayBufferView & {
      length: number;
      [k: number]: unknown;
    };
    const length = aView.length;
    if (length !== bView.length) {
      return false;
    }
    for (let i = length; i-- !== 0; ) {
      if (aView[i] !== bView[i]) {
        return false;
      }
    }
    return true;
  }

  if (a instanceof RegExp) {
    const bRe = b as RegExp;
    return a.source === bRe.source && a.flags === bRe.flags;
  }

  if (aObj.valueOf !== Object.prototype.valueOf) {
    return aObj.valueOf() === bObj.valueOf();
  }
  if (aObj.toString !== Object.prototype.toString) {
    return aObj.toString() === bObj.toString();
  }

  const keys = Object.keys(aObj);
  const length = keys.length;
  if (length !== Object.keys(bObj).length) {
    return false;
  }

  for (let i = length; i-- !== 0; ) {
    if (!Object.prototype.hasOwnProperty.call(b, keys[i]!)) {
      return false;
    }
  }

  for (let i = length; i-- !== 0; ) {
    const key = keys[i]!;
    if (!equal(aObj[key], bObj[key])) {
      return false;
    }
  }

  return true;
}
