export interface QueryParams {
  [key: string]: QueryParamValue;
}

export type QueryParamValue = string | QueryParamValue[] | QueryParams;

const ARRAY_LIMIT = 20;
const APPEND_ARRAY = Symbol('appendArray');
const ARRAY_INDEX_PATTERN = /^\d+$/;
const UNSAFE_KEYS = new Set(['__proto__', 'constructor']);

type QueryParamArray = QueryParamValue[] & {
  [APPEND_ARRAY]?: true;
};

function isQueryParams(
  value: QueryParamValue | undefined
): value is QueryParams {
  return typeof value === 'object' && value != null && !Array.isArray(value);
}

function isAppendArray(value: QueryParamValue[]) {
  return (value as QueryParamArray)[APPEND_ARRAY] === true;
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
  const segmentPattern = /([^[\]]+)|\[([^\]]*)\]/g;
  let match = segmentPattern.exec(key);

  while (match) {
    segments.push(match[1] ?? match[2] ?? '');
    match = segmentPattern.exec(key);
  }

  return segments;
}

function arrayToObject(values: QueryParamValue[]) {
  const result: QueryParams = {};

  values.forEach((value, index) => {
    result[index.toString()] = value;
  });

  return result;
}

function mergeArrays(existing: QueryParamValue[], incoming: QueryParamValue[]) {
  if (isAppendArray(existing) || isAppendArray(incoming)) {
    return existing.concat(incoming);
  }

  const result = existing.slice();

  incoming.forEach((value, index) => {
    const currentValue = result[index];

    result[index] =
      currentValue == null ? value : mergeValues(currentValue, value);
  });

  return result;
}

function mergeObjects(existing: QueryParams, incoming: QueryParams) {
  const result: QueryParams = { ...existing };

  Object.entries(incoming).forEach(([key, value]) => {
    if (isUnsafeKey(key)) {
      return;
    }

    result[key] = mergeValues(result[key], value);
  });

  return result;
}

function mergeExistingArray(
  existing: QueryParamValue[],
  incoming: QueryParamValue
) {
  if (Array.isArray(incoming)) {
    return mergeArrays(existing, incoming);
  }

  if (isQueryParams(incoming)) {
    return mergeObjects(arrayToObject(existing), incoming);
  }

  return existing.concat(incoming);
}

function mergeIncomingArray(
  existing: QueryParamValue,
  incoming: QueryParamValue[]
) {
  if (isQueryParams(existing)) {
    return mergeObjects(existing, arrayToObject(incoming));
  }

  return [existing, ...incoming];
}

function mergeValues(
  existing: QueryParamValue | undefined,
  incoming: QueryParamValue
): QueryParamValue {
  if (existing == null) {
    return incoming;
  }

  if (Array.isArray(existing)) {
    return mergeExistingArray(existing, incoming);
  }

  if (Array.isArray(incoming)) {
    return mergeIncomingArray(existing, incoming);
  }

  if (isQueryParams(existing) && isQueryParams(incoming)) {
    return mergeObjects(existing, incoming);
  }

  return [existing, incoming];
}

function buildQueryParam(segments: string[], value: string) {
  let result: QueryParamValue = value;

  for (let i = segments.length - 1; i >= 0; i--) {
    const segment = segments[i];

    if (segment === '') {
      const arrayValue: QueryParamArray = [result];
      arrayValue[APPEND_ARRAY] = true;
      result = arrayValue;
    } else if (isArrayIndex(segment)) {
      const arrayValue: QueryParamValue[] = [];
      arrayValue[Number(segment)] = result;
      result = arrayValue;
    } else {
      result = {
        [segment]: result,
      };
    }
  }

  return result;
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
  let result: QueryParams = {};
  const search = queryString.startsWith('?')
    ? queryString.substring(1)
    : queryString;

  new URLSearchParams(search).forEach((value, key) => {
    const segments = parseKey(key);

    if (segments.length === 0 || segments.some(isUnsafeKey)) {
      return;
    }

    const queryParam = buildQueryParam(segments, value);

    if (Array.isArray(queryParam)) {
      result = mergeObjects(result, arrayToObject(queryParam));
    } else if (isQueryParams(queryParam)) {
      result = mergeObjects(result, queryParam);
    }
  });

  return compactQueryParam(result) as QueryParams;
}
