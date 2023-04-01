using BeGeneric.Context;
using BeGeneric.GenericModels;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace BeGeneric.Services.BeGeneric
{
    public interface IAttachedActionService
    {
        Func<ActionData<T>, Task> GetAttachedAction<T>(string controllerName, ActionType actionType, ActionOrderType actionOrder);
        Func<ActionData, Task> GetAttachedAction(string controllerName, ActionType actionType, ActionOrderType actionOrder);

        void RegisterAttachedAction<T>(string controllerName, ActionType actionType, ActionOrderType actionOrder, Func<ActionData<T>, Task> action);
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

    public class ActionData
    {
        public Guid Id { get; set; }
        public int? Page { get; set; } 
        public int PageSize { get; set; }
        public string SortProperty { get; set; }
        public string SortOrder { get; set; }
        public ComparerObject FilterObject { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }

        internal string GetAllResultData { get; set; }
        internal string GetOneResultData { get; set; }
        internal string InputParameterData { get; set; }
        internal string SavedParameterData { get; set; }

        public EntityDbContext Context { get; set; }
    }

    public class ActionData<T> : ActionData
    {
        public ActionData(ActionData baseData)
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

            Context = baseData.Context;
        }

        private bool isGetAllResultSet = false;
        private PagedResult<T> getAllResult = null;
        public PagedResult<T> GetAllResult
        {
            get
            {
                if (!isGetAllResultSet)
                {
                    try
                    {
                        getAllResult = JsonSerializer.Deserialize<PagedResult<T>>(GetAllResultData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
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
        private T getOneResult = default;
        public T GetOneResult
        {
            get
            {
                if (!isGetOneResultSet)
                {
                    try
                    {
                        getOneResult = JsonSerializer.Deserialize<T>(GetOneResultData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
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
        private T inputParameter = default;
        public T InputParameter
        {
            get
            {
                if (!isInputParameterSet)
                {
                    try
                    {
                        inputParameter = JsonSerializer.Deserialize<T>(InputParameterData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
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
        private T savedParameter = default;
        public T SavedParameter
        {
            get
            {
                if (!isSavedParameterSet)
                {
                    try
                    {
                        savedParameter = JsonSerializer.Deserialize<T>(InputParameterData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
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
