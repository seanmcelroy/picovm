namespace picovm.Packager.PE
{
    public enum PEDataDictionaryIndex
    {
        EXPORT_TABLE = 0,
        IMPORT_TABLE = 1,
        RESOURCE_TABLE = 2,
        EXCEPTION_TABLE = 3,
        CERTIFICATE_TABLE = 4,
        BASE_RELOCATION_TABLE = 5,
        DEBUG = 6,
        ARCHITECTURE = 7,
        GLOBAL_POINTER = 8,
        TLS_TABLE = 9,
        LOAD_CONFIGURATION_TABLE = 10,
        BOUND_IMPORT = 11,
        IMPORT_ADDRESS_TABLE = 12,
        DELAY_IMPORT_DESCRIPTOR = 13,
        CLR_RUNTIME_HEADER = 14,
        RESERVED_15 = 15
    }
}