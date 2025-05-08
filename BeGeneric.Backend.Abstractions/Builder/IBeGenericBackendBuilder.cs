using BeGeneric.Backend.Common.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BeGeneric.Backend.Common.Builder;

public interface IBeGenericBackendBuilder
{
    IServiceCollection Services { get; }
    BeConfiguration Configuration { get; }
    Type PrimaryKeyType { get; }
}