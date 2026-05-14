export interface QueryParams {
  [key: string]: QueryParamValue;
}

export type QueryParamValue = string | QueryParamValue[] | QueryParams;

type QueryContainer = QueryParamValue[] | QueryParams;
type QueryArrayObject = QueryParamValue[] & QueryParams;

const ARRAY_LIMIT = 20;
const ARRAY_INDEX_PATTERN = /^\d+$/;
const ARRAY_OBJECT_PROPERTY_MARKER = '__queryParamProperty__:';
const BRACKET_SEGMENT_PATTERN = /\[[^[\]]*]/g;
const DEPTH_LIMIT = 5;
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

  return (
    ARRAY_INDEX_PATTERN.test(key) &&
    index.toString() === key &&
    index <= ARRAY_LIMIT
  );
}

function getArrayObjectStorageKey(key: string) {
  return `${ARRAY_OBJECT_PROPERTY_MARKER}${key}`;
}

function getArrayObjectResultKey(key: string) {
  return key.substring(ARRAY_OBJECT_PROPERTY_MARKER.length);
}

function getBracketSegment(segment: string) {
  return segment.substring(1, segment.length - 1);
}

function parseKey(key: string) {
  if (key === '') {
    return [];
  }

  const segments: string[] = [];
  BRACKET_SEGMENT_PATTERN.lastIndex = 0;

  let match = BRACKET_SEGMENT_PATTERN.exec(key);

  if (!match) {
    return [key];
  }

  const parentKey = key.substring(0, match.index);

  if (parentKey !== '') {
    segments.push(parentKey);
  }

  while (match && segments.length < DEPTH_LIMIT + 1) {
    segments.push(getBracketSegment(match[0]));
    match = BRACKET_SEGMENT_PATTERN.exec(key);
  }

  if (!match) {
    return segments;
  }

  return segments.concat(key.substring(match.index));
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
      : (container as QueryArrayObject)[getArrayObjectStorageKey(key)];
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
      (container as QueryArrayObject)[getArrayObjectStorageKey(key)] = value;
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
  return Object.keys(value).filter((key) =>
    key.startsWith(ARRAY_OBJECT_PROPERTY_MARKER)
  );
}

function setLeaf(container: QueryContainer, key: string, value: string) {
  const existingValue = getValue(container, key);

  setValue(container, key, mergeValue(existingValue, value));
}

function shouldMergeScalarArrayValue(
  nextKey: string,
  existingValue: QueryParamValue | undefined
) {
  return (
    (nextKey === '' || isArrayIndex(nextKey)) &&
    existingValue != null &&
    !isContainer(existingValue)
  );
}

function setNestedContainer(
  container: QueryContainer,
  key: string,
  nextKey: string
) {
  const existingValue = getValue(container, key);

  if (isContainer(existingValue)) {
    return existingValue;
  }

  const nextContainer = createContainer(nextKey);
  const nextValue =
    existingValue == null
      ? nextContainer
      : mergeValue(existingValue, nextContainer);

  setValue(container, key, nextValue);

  return nextContainer;
}

function assignNonLeaf(
  container: QueryContainer,
  key: string,
  nextKey: string,
  value: string
) {
  const existingValue = getValue(container, key);

  if (shouldMergeScalarArrayValue(nextKey, existingValue)) {
    setValue(container, key, mergeValue(existingValue, value));

    return undefined;
  }

  return setNestedContainer(container, key, nextKey);
}

function assignParam(result: QueryParams, segments: string[], value: string) {
  let container: QueryContainer | undefined = result;

  segments.forEach((key, index) => {
    if (!container) {
      return;
    }

    if (index === segments.length - 1) {
      setLeaf(container, key, value);
      container = undefined;

      return;
    }

    container = assignNonLeaf(container, key, segments[index + 1], value);
  });
}

function compactQueryParam(value: QueryParamValue): QueryParamValue {
  if (Array.isArray(value)) {
    const objectKeys = getArrayObjectKeys(value);

    if (objectKeys.length === 0) {
      return value
        .filter((item) => item != null)
        .map((item) => compactQueryParam(item));
    }

    const result: QueryParams = {};

    value.forEach((item, index) => {
      if (item == null) {
        return;
      }

      result[index.toString()] = compactQueryParam(item);
    });

    objectKeys.forEach((key) => {
      result[getArrayObjectResultKey(key)] = compactQueryParam(
        (value as QueryArrayObject)[key]
      );
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
