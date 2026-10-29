using POS.Contract.Dtos.KitchenDtos;
using POS.Core.Entities.Kitchen;

namespace POS.Core.Services.Contract.KitchenServices;

public interface IKdsService
{
    // Screen Management
    Task<List<KdsScreenDto>> GetScreensAsync(int branchId);
    Task<KdsScreenDto?> GetScreenByIdAsync(int id);
    Task<KdsScreenDto> SaveScreenAsync(KdsScreenDto dto);
    Task<bool> DeleteScreenAsync(int id);

    // KDS Live Feed (Kitchen)
    Task<List<KdsOrderTicketDto>> GetActiveKitchenOrdersAsync(int branchId, int screenId);
    Task<bool> BumpKitchenOrderAsync(int orderId);
    Task<bool> RecallKitchenOrderAsync(int orderId);

    // Pickup Live Feed
    Task<List<KdsOrderTicketDto>> GetActivePickupOrdersAsync(int branchId);
    Task<bool> CompletePickupOrderAsync(int orderId);
}
