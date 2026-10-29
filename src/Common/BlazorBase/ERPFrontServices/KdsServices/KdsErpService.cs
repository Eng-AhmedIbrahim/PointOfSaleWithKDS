using System.Net.Http.Json;
using POS.Contract.Dtos.KitchenDtos;

namespace BlazorBase.ERPFrontServices.KdsServices;

public class KdsErpService : IKdsErpService
{
    private readonly HttpClient _httpClient;

    public KdsErpService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<KdsScreenDto>> GetScreens(int branchId = 0)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<List<KdsScreenDto>>($"api/kds/screens?branchId={branchId}");
            return res ?? new List<KdsScreenDto>();
        }
        catch
        {
            return new List<KdsScreenDto>();
        }
    }

    public async Task<KdsScreenDto?> GetScreenById(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<KdsScreenDto>($"api/kds/screens/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<KdsScreenDto?> SaveScreen(KdsScreenDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/kds/screens", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<KdsScreenDto>();
            }
        }
        catch
        {
        }
        return null;
    }

    public async Task<bool> DeleteScreen(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/kds/screens/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<KdsOrderTicketDto>> GetActiveKitchenOrders(int branchId = 0, int screenId = 0)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<List<KdsOrderTicketDto>>($"api/kds/active-kitchen?branchId={branchId}&screenId={screenId}");
            return res ?? new List<KdsOrderTicketDto>();
        }
        catch
        {
            return new List<KdsOrderTicketDto>();
        }
    }

    public async Task<bool> BumpKitchenOrder(int orderId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/kds/bump-kitchen/{orderId}", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RecallKitchenOrder(int orderId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/kds/recall-kitchen/{orderId}", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<KdsOrderTicketDto>> GetActivePickupOrders(int branchId = 0, int screenId = 0)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<List<KdsOrderTicketDto>>($"api/kds/active-pickup?branchId={branchId}&screenId={screenId}");
            return res ?? new List<KdsOrderTicketDto>();
        }
        catch
        {
            return new List<KdsOrderTicketDto>();
        }
    }

    public async Task<bool> CompletePickupOrder(int orderId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/kds/complete-pickup/{orderId}", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
