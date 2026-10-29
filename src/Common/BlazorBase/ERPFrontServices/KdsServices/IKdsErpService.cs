using POS.Contract.Dtos.KitchenDtos;

namespace BlazorBase.ERPFrontServices.KdsServices;

public interface IKdsErpService
{
    Task<List<KdsScreenDto>> GetScreens(int branchId = 0);
    Task<KdsScreenDto?> GetScreenById(int id);
    Task<KdsScreenDto?> SaveScreen(KdsScreenDto dto);
    Task<bool> DeleteScreen(int id);

    Task<List<KdsOrderTicketDto>> GetActiveKitchenOrders(int branchId = 0, int screenId = 0);
    Task<bool> BumpKitchenOrder(int orderId);
    Task<bool> RecallKitchenOrder(int orderId);

    Task<List<KdsOrderTicketDto>> GetActivePickupOrders(int branchId = 0, int screenId = 0);
    Task<bool> CompletePickupOrder(int orderId);
}
