export interface QueryParams {
  [key: string]: QueryParamValue;
}

export type QueryParamValue = string | QueryParamValue[] | QueryParams;

type QueryContainer = QueryParamValue[] | QueryParams;

const ARRAY_LIMIT = 20;
const ARRAY_INDEX_PATTERN = /^\d+$/;
const KEY_SEGMENT_PATTERN = /([^[\]]+)|\[([^\]]*)\]/g;
const UNSAFE_KEYS = new Set(['__proto__', 'constructor']);

function isQueryParams(
  value: QueryParamValue | undefined
): value is QueryParams {
  return typeof value === 'object' && value != null && !Array.isArray(value);
}

function isContainer(
  value: QueryParamValue | undefined
): value is QueryContainer {
  return Array.isArray(value) || isQueryParams(value);
}

function isUnsafeKey(key: string) {
  return key !== '' && UNSAFE_KEYS.has(key);
}

function isArrayIndex(key: string) {
  const index = Number(key);

  return ARRAY_INDEX_PATTERN.test(key) && index <= ARRAY_LIMIT;
}

function parseKey(key: string) {
  const segments: string[] = [];
  let match = KEY_SEGMENT_PATTERN.exec(key);

  while (match) {
    segments.push(match[1] ?? match[2] ?? '');
    match = KEY_SEGMENT_PATTERN.exec(key);
  }

  return segments;
}

function createContainer(nextKey: string): QueryContainer {
  return nextKey === '' || isArrayIndex(nextKey) ? [] : {};
}

function getValue(container: QueryContainer, key: string) {
  if (Array.isArray(container)) {
    return key === '' ? undefined : container[Number(key)];
  }

  return container[key];
}

function setValue(
  container: QueryContainer,
  key: string,
  value: QueryParamValue
) {
  if (Array.isArray(container)) {
    if (key === '') {
      container.push(value);
    } else if (isArrayIndex(key)) {
      container[Number(key)] = value;
    }

    return;
  }

  container[key] = value;
}

function mergeValue(
  existingValue: QueryParamValue | undefined,
  value: string
): QueryParamValue {
  if (existingValue == null) {
    return value;
  }

  return Array.isArray(existingValue)
    ? existingValue.concat(value)
    : [existingValue, value];
}

function setLeaf(container: QueryContainer, key: string, value: string) {
  const existingValue = getValue(container, key);

  setValue(container, key, mergeValue(existingValue, value));
}

function assignParam(result: QueryParams, segments: string[], value: string) {
  let container: QueryContainer = result;

  for (let i = 0; i < segments.length; i++) {
    const key = segments[i];
    const nextKey = segments[i + 1];

    if (i === segments.length - 1) {
      setLeaf(container, key, value);

      return;
    }

    const existingValue = getValue(container, key);

    if (
      nextKey === '' &&
      existingValue != null &&
      !isContainer(existingValue)
    ) {
      setValue(container, key, mergeValue(existingValue, value));

      return;
    }

    if (isContainer(existingValue)) {
      container = existingValue;
    } else {
      const nextContainer = createContainer(nextKey);
      setValue(container, key, nextContainer);
      container = nextContainer;
    }
  }
}

function compactQueryParam(value: QueryParamValue): QueryParamValue {
  if (Array.isArray(value)) {
    return value
      .filter((item) => item != null)
      .map((item) => compactQueryParam(item));
  }

  if (isQueryParams(value)) {
    const result: QueryParams = {};

    Object.entries(value).forEach(([key, nestedValue]) => {
      result[key] = compactQueryParam(nestedValue);
    });

    return result;
  }

  return value;
}

export default function parseQueryParams(queryString: string) {
  const result: QueryParams = {};
  const search = queryString.startsWith('?')
    ? queryString.substring(1)
    : queryString;

  new URLSearchParams(search).forEach((value, key) => {
    const segments = parseKey(key);

    if (segments.length === 0 || segments.some(isUnsafeKey)) {
      return;
    }

    assignParam(result, segments, value);
  });

  return compactQueryParam(result) as QueryParams;
}
