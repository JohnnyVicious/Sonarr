export interface QueryParams {
  [key: string]: QueryParamValue;
}

export type QueryParamValue = string | QueryParamValue[] | QueryParams;

type QueryContainer = QueryParamValue[] | QueryParams;
type QueryArrayObject = QueryParamValue[] & QueryParams;

const ARRAY_LIMIT = 20;
const ARRAY_INDEX_PATTERN = /^\d+$/;
const DEPTH_LIMIT = 5;
const KEY_SEGMENT_PATTERN = /([^[\]]+)|\[([^\]]*)\]/g;
const PARAMETER_LIMIT = 1000;
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
  return key !== '' && (UNSAFE_KEYS.has(key) || key in Object.prototype);
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

  if (segments.length <= DEPTH_LIMIT + 1) {
    return segments;
  }

  return segments.slice(0, DEPTH_LIMIT + 1).concat(
    segments
      .slice(DEPTH_LIMIT + 1)
      .map((segment) => `[${segment}]`)
      .join('')
  );
}

function createContainer(nextKey: string): QueryContainer {
  return nextKey === '' || isArrayIndex(nextKey) ? [] : {};
}

function getValue(container: QueryContainer, key: string) {
  if (Array.isArray(container)) {
    if (key === '') {
      return undefined;
    }

    return isArrayIndex(key)
      ? container[Number(key)]
      : (container as QueryArrayObject)[key];
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
    } else {
      (container as QueryArrayObject)[key] = value;
    }

    return;
  }

  container[key] = value;
}

function mergeValue(
  existingValue: QueryParamValue | undefined,
  value: QueryParamValue
): QueryParamValue {
  if (existingValue == null) {
    return value;
  }

  return Array.isArray(existingValue)
    ? existingValue.concat(value)
    : [existingValue, value];
}

function getArrayObjectKeys(value: QueryParamValue[]) {
  return Object.keys(value).filter((key) => !isArrayIndex(key));
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
      (nextKey === '' || isArrayIndex(nextKey)) &&
      existingValue != null &&
      !isContainer(existingValue)
    ) {
      setValue(container, key, mergeValue(existingValue, value));

      return;
    }

    if (isContainer(existingValue)) {
      container = existingValue;
    } else if (existingValue == null) {
      const nextContainer = createContainer(nextKey);

      setValue(container, key, nextContainer);
      container = nextContainer;
    } else {
      const nextContainer = createContainer(nextKey);

      setValue(container, key, mergeValue(existingValue, nextContainer));
      container = nextContainer;
    }
  }
}

function compactQueryParam(value: QueryParamValue): QueryParamValue {
  if (Array.isArray(value)) {
    const compactedItems = value
      .filter((item) => item != null)
      .map((item) => compactQueryParam(item));
    const objectKeys = getArrayObjectKeys(value);

    if (objectKeys.length === 0) {
      return compactedItems;
    }

    const result: QueryParams = {};

    compactedItems.forEach((item, index) => {
      result[index.toString()] = item;
    });

    objectKeys.forEach((key) => {
      result[key] = compactQueryParam((value as QueryArrayObject)[key]);
    });

    return result;
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

function limitSearch(search: string) {
  return search.split('&', PARAMETER_LIMIT).join('&');
}

export default function parseQueryParams(queryString: string) {
  const result: QueryParams = {};
  const search = queryString.startsWith('?')
    ? queryString.substring(1)
    : queryString;

  new URLSearchParams(limitSearch(search)).forEach((value, key) => {
    const segments = parseKey(key);

    if (segments.length === 0 || segments.some(isUnsafeKey)) {
      return;
    }

    assignParam(result, segments, value);
  });

  return compactQueryParam(result) as QueryParams;
}
