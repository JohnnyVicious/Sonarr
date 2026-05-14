export interface ScalarQueryParams {
  [key: string]: string;
}

const PARAMETER_LIMIT = 1000;

function isUnsafeKey(key: string) {
  return key in Object.prototype;
}

function limitSearch(search: string) {
  return search.split('&', PARAMETER_LIMIT).join('&');
}

export default function parseScalarQueryParams<T = ScalarQueryParams>(
  queryString: string
) {
  const result: ScalarQueryParams = {};

  new URLSearchParams(limitSearch(queryString)).forEach((value, key) => {
    if (key === '' || isUnsafeKey(key)) {
      return;
    }

    result[key] = value;
  });

  return result as T;
}
