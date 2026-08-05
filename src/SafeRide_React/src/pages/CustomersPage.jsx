import { useMemo, useState } from 'react';
import { useEffect } from 'react';
import { AdminLayout } from '../shared/layouts/AdminLayout';
import useFetch from '../shared/hooks/useFetch';
import useAdminSearch from '../shared/hooks/useAdminSearch';
import { CustomerTable } from '../features/customers/components';
import {
    blockCustomer,
    getCustomersPath,
    mapCustomerList,
    unlockCustomer,
} from '../features/customers/customersApi';
import ActionFeedback from '../shared/components/ActionFeedback/ActionFeedback';
import AccountActionDialog from '../shared/components/AccountActionDialog/AccountActionDialog';
import './CustomersPage.css';

const EMPTY_COUNTS = {
    all: 0,
    active: 0,
    blocked: 0,
    premium: 0,
};

function CustomersPage() {
    const [activeFilter, setActiveFilter] = useState('all');
    const [actionCustomerId, setActionCustomerId] = useState(null);
    const [mutationError, setMutationError] = useState(null);
    const [successMessage, setSuccessMessage] = useState(null);
    const [pendingCustomerAction, setPendingCustomerAction] = useState(null);
    const { query, setQuery } = useAdminSearch({
        placeholder: 'Tìm kiếm khách hàng, ID, email hoặc số điện thoại...',
    });
    const { data, isLoading, error, refetch, setData } = useFetch(getCustomersPath(), {
        select: mapCustomerList,
    });
    const safeCustomers = useMemo(() => data?.customers ?? [], [data]);
    const counts = data?.counts ?? EMPTY_COUNTS;
    const visibleCustomers = useMemo(
        () => safeCustomers.filter((customer) => customerMatchesFilter(customer, activeFilter)
            && customerMatchesSearch(customer, query)),
        [activeFilter, query, safeCustomers],
    );

    useEffect(() => {
        if (!successMessage) {
            return undefined;
        }

        const timeoutId = window.setTimeout(() => {
            setSuccessMessage(null);
        }, 4000);

        return () => {
            window.clearTimeout(timeoutId);
        };
    }, [successMessage]);

    const updateCustomer = (nextCustomer) => {
        const nextCustomers = safeCustomers.map((customer) => (
            customer.id === nextCustomer.id ? nextCustomer : customer
        ));
        setData({
            counts: calculateCustomerCounts(nextCustomers),
            customers: nextCustomers,
        });
    };

    const closeAccountActionDialog = () => {
        if (actionCustomerId) {
            return;
        }

        setMutationError(null);
        setPendingCustomerAction(null);
    };

    const handleConfirmCustomerAction = async (payload = {}) => {
        const customer = pendingCustomerAction?.customer;
        const actionMode = pendingCustomerAction?.mode;
        if (!customer || !actionMode) {
            return;
        }

        setActionCustomerId(customer.id);
        setMutationError(null);

        try {
            const nextCustomer = actionMode === 'lock'
                ? await blockCustomer(customer.id, payload.reason)
                : await unlockCustomer(customer.id);
            updateCustomer(nextCustomer);
            refetch();
            setSuccessMessage(
                actionMode === 'lock'
                    ? 'Đã khóa tài khoản khách hàng thành công.'
                    : 'Đã mở khóa tài khoản khách hàng thành công.',
            );
            setPendingCustomerAction(null);
        } catch (caughtError) {
            setMutationError(
                caughtError instanceof Error
                    ? caughtError.message
                    : 'KhÃ´ng thá»ƒ cáº­p nháº­t tÃ i khoáº£n khÃ¡ch hÃ ng.',
            );
        } finally {
            setActionCustomerId(null);
        }
    };

    const handleToggleBlock = async (customer) => {
        if (isValidatedCustomerAccountFlowEnabled()) {
            setMutationError(null);
            setPendingCustomerAction({
                mode: customer.isActive ? 'lock' : 'unlock',
                customer,
            });
            return;
        }

        const reason = customer.isActive
            ? window.prompt('Lý do khóa khách hàng?', customer.banReason ?? '')
            : null;

        if (customer.isActive && reason === null) {
            return;
        }

        setActionCustomerId(customer.id);
        setMutationError(null);

        try {
            const nextCustomer = customer.isActive
                ? await blockCustomer(customer.id, reason ?? undefined)
                : await unlockCustomer(customer.id);
            updateCustomer(nextCustomer);
            refetch();
        } catch (caughtError) {
            setMutationError(
                caughtError instanceof Error
                    ? caughtError.message
                    : 'Không thể cập nhật tài khoản khách hàng.',
            );
        } finally {
            setActionCustomerId(null);
        }
    };

    return (
        <AdminLayout>
            <div className="customers-page-header">
                <h1 className="customers-page-title">Quản lý Khách hàng</h1>
                <p className="customers-page-subtitle">
                    Theo dõi danh sách tài khoản khách hàng trên toàn hệ thống SafeRide.
                </p>
            </div>

            <ActionFeedback message={successMessage} />

            {error && (
                <div className="customers-feedback customers-feedback--error">
                    <span>{error}</span>
                    <button type="button" onClick={refetch}>Thử lại</button>
                </div>
            )}

            {mutationError && (
                <div className="customers-feedback customers-feedback--error">
                    <span>{mutationError}</span>
                </div>
            )}

            {isLoading && (
                <div className="customers-feedback">
                    Đang tải danh sách khách hàng...
                </div>
            )}

            <CustomerTable
                customers={visibleCustomers}
                totalCustomers={counts.all}
                activeFilter={activeFilter}
                onFilterChange={setActiveFilter}
                onToggleBlock={handleToggleBlock}
                actionCustomerId={actionCustomerId}
                searchQuery={query}
                onSearchChange={setQuery}
            />

            <AccountActionDialog
                key={pendingCustomerAction ? `${pendingCustomerAction.mode}-${pendingCustomerAction.customer.id}` : 'customer-account-action-closed'}
                isOpen={Boolean(pendingCustomerAction)}
                mode={pendingCustomerAction?.mode}
                accountType="customer"
                accountName={pendingCustomerAction?.customer?.name}
                currentReason={pendingCustomerAction?.customer?.banReason}
                isSubmitting={actionCustomerId === pendingCustomerAction?.customer?.id}
                errorMessage={pendingCustomerAction ? mutationError : null}
                onClose={closeAccountActionDialog}
                onConfirm={handleConfirmCustomerAction}
            />
        </AdminLayout>
    );
}

function customerMatchesFilter(customer, filter) {
    if (filter === 'all') {
        return true;
    }

    if (filter === 'premium') {
        return customer.tier === 'premium';
    }

    return customer.status === filter;
}

function customerMatchesSearch(customer, query) {
    const normalizedQuery = normalizeSearchQuery(query);
    if (!normalizedQuery) {
        return true;
    }

    const digitQuery = normalizeDigits(query);
    const searchableValues = [
        customer.name,
        customer.email,
        customer.phone,
        customer.id,
        customer.customerCode,
    ];

    return searchableValues
        .map(normalizeSearchQuery)
        .some((value) => value.includes(normalizedQuery))
        || (digitQuery.length > 0 && normalizeDigits(customer.phone).includes(digitQuery));
}

function calculateCustomerCounts(customers) {
    return {
        all: customers.length,
        active: customers.filter((customer) => customer.status === 'active').length,
        blocked: customers.filter((customer) => customer.status === 'blocked').length,
        premium: customers.filter((customer) => customer.tier === 'premium').length,
    };
}

function normalizeSearchQuery(value) {
    return String(value ?? '').trim().toLocaleLowerCase('vi-VN');
}

function normalizeDigits(value) {
    return String(value ?? '').replace(/\D/g, '');
}

function isValidatedCustomerAccountFlowEnabled() {
    return typeof window !== 'undefined';
}

export default CustomersPage;
