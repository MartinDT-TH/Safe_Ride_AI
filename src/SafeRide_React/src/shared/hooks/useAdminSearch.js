import { useEffect } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import {
    resetHeaderSearch,
    setHeaderSearchPlaceholder,
    setHeaderSearchQuery,
} from '../../features/ui/uiSlice';

function useAdminSearch({ placeholder }) {
    const dispatch = useAppDispatch();
    const query = useAppSelector((state) => state.ui.headerSearchQuery);

    useEffect(() => {
        dispatch(setHeaderSearchPlaceholder(placeholder));
        dispatch(setHeaderSearchQuery(''));

        return () => {
            dispatch(resetHeaderSearch());
        };
    }, [dispatch, placeholder]);

    const setQuery = (value) => {
        dispatch(setHeaderSearchQuery(value));
    };

    return { query, setQuery };
}

export default useAdminSearch;
