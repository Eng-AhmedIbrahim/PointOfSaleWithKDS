namespace POS.Authorization.Models;

public static class Permissions
{
    public static readonly Dictionary<string, string> RolePermissions = new()
    {
        // â”€â”€ Nav / Screens â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanAccessTables",       "CanAccessTables" },
        { "CanAccessDelivery",     "CanAccessDelivery" },
        { "CanAccessTakeAway",     "CanAccessTakeAway" },
        { "CanAccessAccounts",     "CanAccessAccounts" },
        { "CanAccessSummary",      "CanAccessSummary" },
        { "CanAccessOrders",       "CanAccessOrders" },
        { "CanAccessDistribution", "CanAccessDistribution" },
        { "CanAccessWaiting",      "CanAccessWaiting" },

        // â”€â”€ Section 3 â€“ Item Actions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanUseKeypad",          "CanUseKeypad" },
        { "CanIncrementQuantity",  "CanIncrementQuantity" },
        { "CanDecrementQuantity",  "CanDecrementQuantity" },
        { "CanDeleteItem",         "CanDeleteItem" },
        { "CanApplyItemDiscount",  "CanApplyItemDiscount" },
        { "CanEditItemComment",    "CanEditItemComment" },

        // â”€â”€ Section 4 â€“ Order Actions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanPrintOrder",    "CanPrintOrder" },
        { "CanWaitingOrder",  "CanWaitingOrder" },
        { "CanCancelOrder",   "CanCancelOrder" },

        // â”€â”€ Footer Buttons â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanAccessFooterDiscountBtn",      "CanAccessFooterDiscountBtn" },
        { "CanAccessFooterCustomerDataBtn",  "CanAccessFooterCustomerDataBtn" },
        { "CanAccessFooterPaymentMethodBtn", "CanAccessFooterPaymentMethodBtn" },
        { "CanAccessFooterQuickPaymentBtn",  "CanAccessFooterQuickPaymentBtn" },
        { "CanAccessFooterMealsBtn",         "CanAccessFooterMealsBtn" },
        { "CanAccessFooterHospitalityBtn",   "CanAccessFooterHospitalityBtn" },
        { "CanAccessFooterWaitingBtn",       "CanAccessFooterWaitingBtn" },
        { "CanAccessFooterSettingsBtn",      "CanAccessFooterSettingsBtn" },

        // â”€â”€ DineIn Buttons â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanAccessDineInOrderBtn",       "CanAccessDineInOrderBtn" },
        { "CanAccessDineInReceiptBtn",     "CanAccessDineInReceiptBtn" },
        { "CanAccessDineInCloseTableBtn",  "CanAccessDineInCloseTableBtn" },
        { "CanAccessDineInSplitOrderBtn",  "CanAccessDineInSplitOrderBtn" },
        { "CanAccessDineInMergeTableBtn",  "CanAccessDineInMergeTableBtn" },
        { "CanAccessDineInTransferBtn",    "CanAccessDineInTransferBtn" },
        { "CanAccessDineInVoidBtn",        "CanAccessDineInVoidBtn" },
        { "CanAccessDineInGuestCountBtn",  "CanAccessDineInGuestCountBtn" },

        // â”€â”€ Delivery Buttons â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanAccessDeliveryOrderBtn",              "CanAccessDeliveryOrderBtn" },
        { "CanAccessDeliveryAddNewBtn",             "CanAccessDeliveryAddNewBtn" },
        { "CanAccessDeliveryClearBtn",              "CanAccessDeliveryClearBtn" },
        { "CanAccessDeliveryComplaintsBtn",         "CanAccessDeliveryComplaintsBtn" },
        { "CanAccessDeliverySearchBtn",             "CanAccessDeliverySearchBtn" },
        { "CanAccessDeliveryBranchManagementBtn",   "CanAccessDeliveryBranchManagementBtn" },
        { "CanAccessDeliveryDistributionBtn",       "CanAccessDeliveryDistributionBtn" },
        { "CanAccessDeliveryToggleDirectionBtn",    "CanAccessDeliveryToggleDirectionBtn" },
        { "CanAccessDeliveryVoidBtn",               "CanAccessDeliveryVoidBtn" },
        { "CanAccessDeliveryEditBtn",               "CanAccessDeliveryEditBtn" },

        // â”€â”€ All Orders (Today) Actions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanViewOrderDetails",          "CanViewOrderDetails" },
        { "CanPrintOrderCustomerReceipt", "CanPrintOrderCustomerReceipt" },
        { "CanPrintOrderKitchenReceipt",  "CanPrintOrderKitchenReceipt" },
        { "CanVoidOrderFromList",         "CanVoidOrderFromList" },

        // â”€â”€ Distribution Buttons â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanAccessDistributionAssignBtn",           "CanAccessDistributionAssignBtn" },
        { "CanAccessDistributionViewBtn",             "CanAccessDistributionViewBtn" },
        { "CanAccessDistributionVoidBtn",             "CanAccessDistributionVoidBtn" },
        { "CanAccessDistributionPrintBtn",            "CanAccessDistributionPrintBtn" },
        { "CanAccessDistributionUnDispatchBtn",       "CanAccessDistributionUnDispatchBtn" },
        { "CanAccessDistributionCollectBtn",          "CanAccessDistributionCollectBtn" },
        { "CanAccessDistributionVoidHistoryBtn",      "CanAccessDistributionVoidHistoryBtn" },
        { "CanAccessDistributionDriverSettlementBtn", "CanAccessDistributionDriverSettlementBtn" },
        { "CanAccessDistributionViewDriversBtn",      "CanAccessDistributionViewDriversBtn" },

        // â”€â”€ Waiting Page Actions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanCompleteWaitingOrder", "CanCompleteWaitingOrder" },
        { "CanRemoveWaitingOrder",   "CanRemoveWaitingOrder" },

        // â”€â”€ Summary Actions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanViewSummaryDetails", "CanViewSummaryDetails" },
        { "CanViewSalesItems", "CanViewSalesItems" },
        { "CanPrintSalesItems", "CanPrintSalesItems" },
        { "CanViewDetailedSales", "CanViewDetailedSales" },
        { "CanPrintSummaryReport", "CanPrintSummaryReport" },

        // â”€â”€ Accounts Actions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanViewStaffAccounts",  "CanViewStaffAccounts" },
        { "CanPrintStaffAccounts", "CanPrintStaffAccounts" },

        // â”€â”€ Global â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanAccessPosSettingsFeature", "CanAccessPosSettingsFeature" },

        // â”€â”€ BackOffice Screens â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanViewDashboardAtBackOffice", "CanViewDashboardAtBackOffice" },
        { "CanManageCompanySettingsAtBackOffice", "CanManageCompanySettingsAtBackOffice" },
        { "CanManageOrderSettingsAtBackOffice", "CanManageOrderSettingsAtBackOffice" },
        { "CanManagePrinterSettingsAtBackOffice", "CanManagePrinterSettingsAtBackOffice" },
        { "CanManageSystemFeaturesAtBackOffice", "CanManageSystemFeaturesAtBackOffice" },
        { "CanSyncDataAtBackOffice", "CanSyncDataAtBackOffice" },
        { "CanManageGeneralSettingsAtBackOffice", "CanManageGeneralSettingsAtBackOffice" },
        { "CanManagePosSettingsAtBackOffice", "CanManagePosSettingsAtBackOffice" },
        { "CanManageLanguageSettingsAtBackOffice", "CanManageLanguageSettingsAtBackOffice" },
        { "CanManageUsersAtBackOffice", "CanManageUsersAtBackOffice" },
        { "CanResetUserPasswordAtBackOffice", "CanResetUserPasswordAtBackOffice" },
        { "CanChangeOwnPasswordAtBackOffice", "CanChangeOwnPasswordAtBackOffice" },
        { "CanManageRolesAtBackOffice", "CanManageRolesAtBackOffice" },
        { "CanManagePermissionsAtBackOffice", "CanManagePermissionsAtBackOffice" },
        { "CanManageItemsAtBackOffice", "CanManageItemsAtBackOffice" },
        { "CanManageZonesAtBackOffice", "CanManageZonesAtBackOffice" },
        { "CanManageTablesAtBackOffice", "CanManageTablesAtBackOffice" },
        { "CanManagePaymentMethodsAtBackOffice", "CanManagePaymentMethodsAtBackOffice" },
        { "CanManageInventoryAtBackOffice", "CanManageInventoryAtBackOffice" },
        { "CanManageRawMaterialsAtBackOffice", "CanManageRawMaterialsAtBackOffice" },
        { "CanManageSemiFinishedItemsAtBackOffice", "CanManageSemiFinishedItemsAtBackOffice" },
        { "CanManageRecipesAtBackOffice", "CanManageRecipesAtBackOffice" },
        { "CanManageUnitsAtBackOffice", "CanManageUnitsAtBackOffice" },
        { "CanViewBackOfficeTransactionsAtBackOffice", "CanViewBackOfficeTransactionsAtBackOffice" },
        { "CanViewBackOfficeReportsAtBackOffice", "CanViewBackOfficeReportsAtBackOffice" },
        { "CanViewQueriesAtBackOffice", "CanViewQueriesAtBackOffice" },
        { "CanViewRegistrationAtBackOffice", "CanViewRegistrationAtBackOffice" },
        { "CanViewClosingAtBackOffice", "CanViewClosingAtBackOffice" },
        { "CanAssignDriverAtBackOffice", "CanAssignDriverAtBackOffice" },
        { "CanViewOrderAtBackOffice", "CanViewOrderAtBackOffice" },
        { "CanVoidOrderAtBackOffice", "CanVoidOrderAtBackOffice" },
        { "CanPrintOrderAtBackOffice", "CanPrintOrderAtBackOffice" },
        { "CanUnDispatchOrderAtBackOffice", "CanUnDispatchOrderAtBackOffice" },
        { "CanCollectOrderAtBackOffice", "CanCollectOrderAtBackOffice" },
        { "CanViewDriversListAtBackOffice", "CanViewDriversListAtBackOffice" },
        { "CanViewVoidHistoryAtBackOffice", "CanViewVoidHistoryAtBackOffice" },
        { "CanViewDriverSettlementAtBackOffice", "CanViewDriverSettlementAtBackOffice" },
        { "CanPrintKitchenOrderAtBackOffice", "CanPrintKitchenOrderAtBackOffice" },
        { "CanPrintStaffAccountsAtBackOffice", "CanPrintStaffAccountsAtBackOffice" },
        { "CanViewSummaryDetailsAtBackOffice", "CanViewSummaryDetailsAtBackOffice" },
        { "CanPrintSummaryReportAtBackOffice", "CanPrintSummaryReportAtBackOffice" },
        { "CanViewSalesItemsAtBackOffice", "CanViewSalesItemsAtBackOffice" },
        { "CanPrintSalesItemsAtBackOffice", "CanPrintSalesItemsAtBackOffice" },
        { "CanViewStaffMealsReportAtBackOffice", "CanViewStaffMealsReportAtBackOffice" },
        { "CanManageStaffMealConfigsAtBackOffice", "CanManageStaffMealConfigsAtBackOffice" },
        { "CanViewHospitalityReportAtBackOffice", "CanViewHospitalityReportAtBackOffice" },
        { "CanDeleteExpenseAtBackOffice", "CanDeleteExpenseAtBackOffice" },

        // â”€â”€ Branch Delivery Orders Page â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        { "CanViewBranchDeliveryOrderDetails", "CanViewBranchDeliveryOrderDetails" },
        { "CanPrintBranchCustomerReceipt",     "CanPrintBranchCustomerReceipt" },
        { "CanPrintBranchDeliveryReceipt",     "CanPrintBranchDeliveryReceipt" },
        { "CanVoidBranchDeliveryOrder",        "CanVoidBranchDeliveryOrder" },
    };
}
