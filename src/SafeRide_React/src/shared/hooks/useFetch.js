import { useCallback, useEffect, useState } from 'react';
import { apiRequest } from '../api/apiClient';
function useFetch(path, options = {}) {
    const { select } = options;
    const [reloadKey, setReloadKey] = useState(0);
    const [state, setState] = useState({
        data: null,
        isLoading: Boolean(path),
        error: null,
    });
    const refetch = useCallback(() => {
        setReloadKey((current) => current + 1);
    }, []);
    const setData = useCallback((nextData) => {
        setState((current) => ({
            ...current,
            data: nextData,
            error: null,
        }));
    }, []);
    useEffect(() => {
        if (!path) {
            return;
        }
        const controller = new AbortController();
        apiRequest(path, { signal: controller.signal })
            .then((data) => {
            setState({ data: select ? select(data) : data, isLoading: false, error: null });
        })
            .catch((error) => {
            if (error.name === 'AbortError') {
                return;
            }
            setState((current) => ({
                ...current,
                isLoading: false,
                error: error.message,
            }));
        });
        return () => controller.abort();
    }, [path, reloadKey, select]);
    return {
        ...state,
        refetch,
        setData,
    };
}
export default useFetch;
