import _ from 'lodash';

export interface QueryParams {
  [key: string]: QueryParamValue;
}

export type QueryParamValue = string | QueryParamValue[] | QueryParams;

const KEY_SEGMENT_PATTERN = /([^[\]]+)|\[([^\]]*)\]/g;
const UNSAFE_KEYS = new Set(['__proto__', 'constructor']);

function isQueryParams(
  value: QueryParamValue | undefined
): value is QueryParams {
  return typeof value === 'object' && value != null && !Array.isArray(value);
}

function isUnsafeKey(key: string) {
  return key !== '' && UNSAFE_KEYS.has(key);
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

function getAppendIndex(result: QueryParams, path: string[]) {
  const existingValue = _.get(result, path);

  return Array.isArray(existingValue) ? existingValue.length.toString() : '0';
}

function normalizePath(result: QueryParams, segments: string[]) {
  return segments.reduce<string[]>((path, segment) => {
    path.push(segment === '' ? getAppendIndex(result, path) : segment);

    return path;
  }, []);
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

function compactQueryParam(value: QueryParamValue): QueryParamValue {
  if (Array.isArray(value)) {
    return value
      .filter((item) => item != null)
      .map((item) => compactQueryParam(item));
  }

  if (isQueryParams(value)) {
    return _.mapValues(value, (nestedValue) =>
      compactQueryParam(nestedValue)
    ) as QueryParams;
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

    const path = normalizePath(result, segments);
    const existingValue = _.get(result, path) as QueryParamValue | undefined;

    _.set(result, path, mergeValue(existingValue, value));
  });

  return compactQueryParam(result) as QueryParams;
}
