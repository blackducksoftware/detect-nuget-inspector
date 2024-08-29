namespace SCA.Detect.Nuget.Inspector.Inspection.Util
{

    static class ExcludedDependencyTypeUtil
    {
        public static bool isDependencyTypeExcluded(String excludedDependencyTypes, String dependencyType)
        {

            if (excludedDependencyTypes.Equals(dependencyType))
            {
                return true;
            }

            return false;
        }
    }
}