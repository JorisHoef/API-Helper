namespace JorisHoef.API.Services.Base
{
    public class GetApiService<TResponse> : ApiService<TResponse>
    {
        protected override HttpMethod HttpMethod => HttpMethod.GET;
    }
}