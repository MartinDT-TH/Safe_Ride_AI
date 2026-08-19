import { useCallback, useEffect } from "react";
import { useAppDispatch, useAppSelector } from "../../app/hooks";
import { selectApiResource, updateApiResource } from "../../slices/apiSlice";
import { fetchApiResource } from "../../thunks/apiThunks";

function useFetch(path, options = {}) {
  const { select } = options;
  const dispatch = useAppDispatch();
  const requestKey = path ?? "__disabled__";
  const resource = useAppSelector((state) =>
    selectApiResource(state, requestKey),
  );
  const refetch = useCallback(() => {
    if (!path) return Promise.resolve();
    return dispatch(fetchApiResource({ path, requestKey }));
  }, [dispatch, path, requestKey]);

  const setData = useCallback(
    (nextData) => {
      dispatch(updateApiResource({ requestKey, data: nextData }));
    },
    [dispatch, requestKey],
  );

  useEffect(() => {
    if (!path) return undefined;
    const request = dispatch(fetchApiResource({ path, requestKey }));
    return () => request.abort();
  }, [dispatch, path, requestKey]);

  const data =
    select && resource.data !== null ? select(resource.data) : resource.data;

  return {
    data,
    isLoading:
      resource.status === "loading" ||
      (Boolean(path) && resource.status === "idle"),
    error: resource.error,
    refetch,
    setData,
  };
}
export default useFetch;
