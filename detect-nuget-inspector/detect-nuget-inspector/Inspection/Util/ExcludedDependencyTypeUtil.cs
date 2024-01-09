namespace Synopsys.Detect.Nuget.Inspector.Inspection.Util
{

    static class ExcludedDependencyTypeUtil
    {
        public static bool isDependencyTypeExcluded(String excludedDependencyTypes)
        {

            if (excludedDependencyTypes == "DEV")
            {
                return true;
            }

            return false;
        }
    }
}