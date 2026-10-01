using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClothingStore.Contracts;
using ClothingStore.Contracts.Account;
using ClothingStore.Contracts.Cart;
using ClothingStore.Contracts.Catalog;
using ClothingStore.Contracts.Orders;
using ClothingStore.Contracts.Store;
using Microsoft.AspNetCore.Http.Extensions;

namespace ClothingStore.Catalog.ApiClients;

/// <summary>Every call the Catalog makes to ClothingStore.Api. The Catalog never touches the database.</summary>
public sealed class StorefrontApi(HttpClient http, ILogger<StorefrontApi> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // ---- Store & catalog ----

    public Task<StoreSettingsDto?> GetStoreAsync(CancellationToken ct) => GetOrNullAsync<StoreSettingsDto>("api/store/settings", ct);

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct) =>
        await GetOrNullAsync<List<CategoryDto>>("api/categories", ct) ?? [];

    public async Task<PagedResult<ProductSummaryDto>> SearchProductsAsync(ProductSearchRequest request, CancellationToken ct)
    {
        var query = new QueryBuilder();
        if (!string.IsNullOrWhiteSpace(request.Category)) query.Add("category", request.Category);
        if (!string.IsNullOrWhiteSpace(request.Search)) query.Add("search", request.Search);
        foreach (var size in request.Sizes) query.Add("sizes", size);
        foreach (var color in request.Colors) query.Add("colors", color);
        if (request.MinPrice is { } min) query.Add("minPrice", min.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (request.MaxPrice is { } max) query.Add("maxPrice", max.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(request.Sort)) query.Add("sort", request.Sort);
        if (request.FeaturedOnly) query.Add("featuredOnly", "true");
        query.Add("page", request.Page.ToString());
        query.Add("pageSize", request.PageSize.ToString());

        return await GetOrNullAsync<PagedResult<ProductSummaryDto>>("api/products" + query, ct)
               ?? new PagedResult<ProductSummaryDto>([], 1, request.PageSize, 0);
    }

    public async Task<ProductFiltersDto> GetFiltersAsync(string? category, CancellationToken ct) =>
        await GetOrNullAsync<ProductFiltersDto>("api/products/filters" + (category is null ? "" : $"?category={Uri.EscapeDataString(category)}"), ct)
        ?? new ProductFiltersDto([], [], 0, 0);

    public Task<ProductDetailDto?> GetProductAsync(string slug, CancellationToken ct) =>
        GetOrNullAsync<ProductDetailDto>($"api/products/{Uri.EscapeDataString(slug)}", ct);

    // ---- Account ----

    public Task<ApiResult<None>> RegisterAsync(RegisterRequest request, CancellationToken ct) => SendAsync<None>(HttpMethod.Post, "api/account/register", request, ct);
    public Task<ApiResult<None>> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct) => SendAsync<None>(HttpMethod.Post, "api/account/confirm-email", request, ct);
    public Task<ApiResult<None>> ResendConfirmationAsync(string email, CancellationToken ct) => SendAsync<None>(HttpMethod.Post, "api/account/resend-confirmation", new EmailRequest { Email = email }, ct);
    public Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct) => SendAsync<AuthResponse>(HttpMethod.Post, "api/account/login", request, ct, allowUnauthorized: true);
    public Task<ApiResult<None>> ForgotPasswordAsync(string email, CancellationToken ct) => SendAsync<None>(HttpMethod.Post, "api/account/forgot-password", new EmailRequest { Email = email }, ct);
    public Task<ApiResult<None>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct) => SendAsync<None>(HttpMethod.Post, "api/account/reset-password", request, ct);

    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(CancellationToken ct) => await GetOrNullAsync<List<AddressDto>>("api/account/addresses", ct) ?? [];
    public Task<ApiResult<AddressDto>> AddAddressAsync(AddressRequest request, CancellationToken ct) => SendAsync<AddressDto>(HttpMethod.Post, "api/account/addresses", request, ct);
    public Task<ApiResult<None>> DeleteAddressAsync(int id, CancellationToken ct) => SendAsync<None>(HttpMethod.Delete, $"api/account/addresses/{id}", null, ct);

    // ---- Cart ----

    public async Task<CartDto> GetCartAsync(CancellationToken ct) => await GetOrNullAsync<CartDto>("api/cart", ct) ?? CartDto.Empty;
    public Task<ApiResult<CartDto>> AddToCartAsync(int variantId, int quantity, CancellationToken ct) =>
        SendAsync<CartDto>(HttpMethod.Post, "api/cart/items", new AddCartItemRequest { VariantId = variantId, Quantity = quantity }, ct);
    public Task<ApiResult<CartDto>> UpdateCartItemAsync(int variantId, int quantity, CancellationToken ct) =>
        SendAsync<CartDto>(HttpMethod.Put, $"api/cart/items/{variantId}", new UpdateCartItemRequest { Quantity = quantity }, ct);
    public Task<ApiResult<CartDto>> MergeCartAsync(CancellationToken ct) => SendAsync<CartDto>(HttpMethod.Post, "api/cart/merge", null, ct);

    // ---- Orders & payments ----

    public Task<ApiResult<PlaceOrderResponse>> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct) => SendAsync<PlaceOrderResponse>(HttpMethod.Post, "api/orders", request, ct);
    public Task<ApiResult<RazorpayCheckoutDto>> GetPaymentAsync(string orderNo, CancellationToken ct) => SendAsync<RazorpayCheckoutDto>(HttpMethod.Get, $"api/orders/{Uri.EscapeDataString(orderNo)}/payment", null, ct);
    public Task<ApiResult<None>> VerifyPaymentAsync(VerifyPaymentRequest request, CancellationToken ct) => SendAsync<None>(HttpMethod.Post, "api/payments/verify", request, ct);

    public async Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(int page, CancellationToken ct) =>
        await GetOrNullAsync<PagedResult<OrderSummaryDto>>($"api/orders?page={page}", ct) ?? new PagedResult<OrderSummaryDto>([], 1, 10, 0);

    public Task<OrderDetailDto?> GetOrderAsync(string orderNo, CancellationToken ct) => GetOrNullAsync<OrderDetailDto>($"api/orders/{Uri.EscapeDataString(orderNo)}", ct);

    // ---- Plumbing ----

    /// <summary>GET; 404 => null. 401 => <see cref="ApiUnauthorizedException"/>.</summary>
    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new ApiUnauthorizedException();

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct, bool allowUnauthorized = false)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);

        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(None) || response.StatusCode == HttpStatusCode.NoContent)
                return ApiResult<T>.Ok(default!);
            return ApiResult<T>.Ok((await response.Content.ReadFromJsonAsync<T>(Json, ct))!);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized && !allowUnauthorized)
            throw new ApiUnauthorizedException();
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return ApiResult<T>.Fail("Too many attempts. Please wait a minute and try again.");

        return ApiResult<T>.Fail(await ReadErrorAsync(response, ct));
    }

    /// <summary>ProblemDetails "detail", or the first validation error.</summary>
    private async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(Json, ct);
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
                return problem.Detail;
            if (problem?.Errors?.Values.SelectMany(v => v).FirstOrDefault() is { } validation)
                return validation;
        }
        catch (JsonException)
        {
        }

        logger.LogWarning("Api call {Url} failed with {Status}", response.RequestMessage?.RequestUri, (int)response.StatusCode);
        return "Something went wrong. Please try again.";
    }

    private sealed record ProblemBody(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
