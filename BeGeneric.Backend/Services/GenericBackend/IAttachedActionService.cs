using BeGeneric.GenericModels;
using System.Text.Json;

namespace BeGeneric.Backend.Services.BeGeneric
{
    public interface IAttachedActionService<T>
    {
        Func<ActionData<G, T>, Task> GetAttachedAction<G>(string controllerName, ActionType actionType, ActionOrderType actionOrder);
        Func<ActionData<T>, Task> GetAttachedAction(string controllerName, ActionType actionType, ActionOrderType actionOrder);

        void RegisterAttachedAction<G>(string controllerName, ActionType actionType, ActionOrderType actionOrder, Func<ActionData<G, T>, Task> action);
    }

    [Flags]
    public enum ActionType : byte
    {
        Get = 1,
        GetAll = 2,
        Post = 4,
        Patch = 8,
        Delete = 16
    }

    public enum ActionOrderType : byte
    {
        Before = 1,
        After = 2
    }

    public class ActionData<T>
    {
        public T Id { get; set; }
        public int? Page { get; set; } 
        public int PageSize { get; set; }
        public string? SortProperty { get; set; }
        public string? SortOrder { get; set; }
        public ComparerObject? FilterObject { get; set; }
        public string? UserName { get; set; }
        public string? Role { get; set; }

        internal string? GetAllResultData { get; set; }
        internal string? GetOneResultData { get; set; }
        internal string? InputParameterData { get; set; }
        internal string? SavedParameterData { get; set; }
    }

    public class ActionData<T, G> : ActionData<G>
    {
        public ActionData(ActionData<G> baseData)
        {
            Id = baseData.Id;
            Page = baseData.Page;
            PageSize = baseData.PageSize;
            SortProperty = baseData.SortProperty;
            SortOrder = baseData.SortOrder;
            FilterObject = baseData.FilterObject;

            UserName = baseData.UserName;
            Role = baseData.Role;

            GetAllResultData = baseData.GetAllResultData;
            GetOneResultData = baseData.GetOneResultData;
            InputParameterData = baseData.InputParameterData;
            SavedParameterData = baseData.SavedParameterData;
        }

        private bool isGetAllResultSet = false;
        private PagedResult<G>? getAllResult = null;
        public PagedResult<G>? GetAllResult
        {
            get
            {
                if (!isGetAllResultSet)
                {
                    try
                    {
                        getAllResult = JsonSerializer.Deserialize<PagedResult<G>>(GetAllResultData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                    }
                    catch
                    {
                        getAllResult = null;
                    }
                    finally
                    {
                        isGetAllResultSet = true;
                    }
                }

                return getAllResult;
            }
        }

        private bool isGetOneResultSet = false;
        private G? getOneResult = default;
        public G? GetOneResult
        {
            get
            {
                if (!isGetOneResultSet)
                {
                    try
                    {
                        getOneResult = JsonSerializer.Deserialize<G>(GetOneResultData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                    }
                    catch
                    {
                        getOneResult = default;
                    }
                    finally
                    {
                        isGetOneResultSet = true;
                    }
                }

                return getOneResult;
            }
        }

        private bool isInputParameterSet = false;
        private G? inputParameter = default;
        public G? InputParameter
        {
            get
            {
                if (!isInputParameterSet)
                {
                    try
                    {
                        inputParameter = JsonSerializer.Deserialize<G>(InputParameterData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                    }
                    catch
                    {
                        inputParameter = default;
                    }
                    finally
                    {
                        isInputParameterSet = true;
                    }
                }

                return inputParameter;
            }
        }

        private bool isSavedParameterSet = false;
        private G? savedParameter = default;
        public G? SavedParameter
        {
            get
            {
                if (!isSavedParameterSet)
                {
                    try
                    {
                        savedParameter = JsonSerializer.Deserialize<G>(InputParameterData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                    }
                    catch
                    {
                        savedParameter = default;
                    }
                    finally
                    {
                        isSavedParameterSet = true;
                    }
                }

                return savedParameter;
            }
        }
    }
}
