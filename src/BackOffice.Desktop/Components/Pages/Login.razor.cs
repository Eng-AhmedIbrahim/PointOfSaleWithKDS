using System.Windows;

namespace BackOffice.Desktop.Components.Pages;

public partial class Login
{
    private string _pin = string.Empty;
    private string _userName = string.Empty;
    private ICollection<UserDto> Users = new List<UserDto>();
    private HttpClient? client;
    private ElementReference pinInput;

    [Inject] private AuthenticationStateProvider _authenticationStateProvider { get; set; } = default!;


    private void AddDigit(string digit)
        => _pin += digit;

    private void ClearInput()
     => _pin = string.Empty;

    private void DeleteLastDigit()
    {
        if (_pin.Length > 0)
            _pin = _pin.Substring(0, _pin.Length - 1);
    }

    protected async override Task OnInitializedAsync()
    {
        // Clear any existing items to avoid NavigationLock behavior on login
        _commonProperties.TableItems = new List<TableItem>();
        _commonProperties.AppendedTableItems = new List<TableItem>();
        _commonProperties.CurrentDineInOrder = null;
        _commonProperties.DineInOrdersDetails = new Dictionary<int, List<DineInOrderDetails>>();
        _commonProperties.DineInOrderValues = new();
        _commonProperties.UpdateDineInOrder = false;
        
        try
        {
            client = _clientFactory.CreateClient(ConstantStrings.ApiUrlName);
            var response = await client.GetAsync(ConstantStrings.GetAllUsersUrl);
            if (response != null && response.IsSuccessStatusCode)
            {
                var usersString = await response.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<UserDto>>(usersString,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                Users = users.OrderByDescending(u => u.UserName == "Administrator").ToList();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add("Failed to load users. Please check connection.", Severity.Error);
        }
        await base.OnInitializedAsync();
    }

    private async Task LoginAction()
    {
        if (string.IsNullOrEmpty(_userName) || _userName == "Select User")
        {
            Snackbar.Add("Please select a user first", Severity.Warning);
            return;
        }

        if (string.IsNullOrEmpty(_pin))
        {
            Snackbar.Add("Please enter your PIN", Severity.Warning);
            return;
        }

        try
        {
            var response = await client!.PostAsJsonAsync(
                ConstantStrings.LoginUserUrl, new { UserName = _userName, Password = _pin, ForBackOffice = true });

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadFromJsonAsync<UserDto>();
                string token = responseContent?.Token ?? string.Empty;

                if (string.IsNullOrEmpty(token))
                {
                    Snackbar.Add("Login Failed: No token received", Severity.Error);
                    return;
                }

                _commonProperties.CurrentUser = !string.IsNullOrWhiteSpace(responseContent?.DisplayName) ? responseContent.DisplayName : (!string.IsNullOrWhiteSpace(responseContent?.UserName) ? responseContent.UserName : _userName);
                _commonProperties.CurrentUserId = responseContent!.UserId;

                //Store token in local storage &update authentication state
                if (_authenticationStateProvider is CustomAuthenticationStateProvider customAuthStateProvider)
                {
                    await customAuthStateProvider.NotifyUserAuthentication(token);
                }

                _commonProperties.TableItems = new List<TableItem>(); // Explicitly clear right before navigation to avoid NavLock
                _commonProperties.NotifyStateChanged();
                
                // Safe navigation for Blazor Hybrid after login
                await SafeNavigateAsync("/");
            }
            else
            {
                Snackbar.Add("فشل تسجيل الدخول: اسم المستخدم أو الرقم السري غير صحيح", Severity.Error);
                _pin = string.Empty;
                StateHasChanged(); 
                await pinInput.FocusAsync();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add("فشل تسجيل الدخول: حدث خطأ أثناء عملية الدخول", Severity.Error);
            _pin = string.Empty;
            StateHasChanged();
            await pinInput.FocusAsync();
        }
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await LoginAction();
        }
    }

    private async Task<List<string>> GetUserPermissions()
    {
        var url = ConstantStrings.GetUserPermissionsUrl; // No userId
        var permissionsResponse = await client!.GetAsync(url);

        if (permissionsResponse.IsSuccessStatusCode)
        {
            var permissionsString = await permissionsResponse.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<string>>(permissionsString) ?? new List<string>();
        }

        return new List<string>();
    }


    private void HandelOnChange(ChangeEventArgs e)
       => _userName = e.Value?.ToString() ?? string.Empty;

    private async Task SafeNavigateAsync(string uri)
    {
        int maxRetries = 5;
        int currentRetry = 0;
        int delayMs = 100;

        while (currentRetry < maxRetries)
        {
            try
            {
                await Task.Delay(delayMs);
                
                // Check if NavigationManager is initialized by checking the Uri property
                if (_navigationManager != null && !string.IsNullOrEmpty(_navigationManager.Uri))
                {
                    // Use InvokeAsync to ensure we're on the correct synchronization context
                    await InvokeAsync(() => _navigationManager.NavigateTo(uri, forceLoad: false));
                    return;
                }
                else
                {
                    throw new InvalidOperationException("NavigationManager not yet initialized");
                }
            }
            catch (InvalidOperationException)
            {
                currentRetry++;
                
                if (currentRetry >= maxRetries)
                {
                    Snackbar.Add("Navigation error after login. Please refresh.", Severity.Error);
                    return;
                }
                
                // Exponential backoff
                delayMs *= 2;
            }
        }
    }

    private void ExitApp()
    {
        Application.Current.Shutdown();
    }
}

