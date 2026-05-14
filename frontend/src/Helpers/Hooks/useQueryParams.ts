import { useMemo } from 'react';
import { useLocation } from 'react-router';
import parseScalarQueryParams from 'Utilities/String/parseScalarQueryParams';

function useQueryParams<T>() {
  const { search } = useLocation();

  return useMemo(() => {
    return parseScalarQueryParams<T>(search);
  }, [search]);
}

export default useQueryParams;
