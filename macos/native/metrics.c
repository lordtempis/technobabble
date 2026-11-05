/* Read-only Mach and IOKit counters; the caller owns all output buffers. */
#include <CoreFoundation/CoreFoundation.h>
#include <IOKit/IOKitLib.h>
#include <mach/mach.h>
#include <stdint.h>
#include <sys/sysctl.h>

int tb_cpu(uint32_t *ticks, int capacity) {
    processor_info_array_t info = NULL;
    natural_t cores = 0;
    mach_msg_type_number_t count = 0;
    mach_port_t host = mach_host_self();
    kern_return_t result = host_processor_info(host, PROCESSOR_CPU_LOAD_INFO, &cores, &info, &count);
    mach_port_deallocate(mach_task_self(), host);
    if (result != KERN_SUCCESS) return 0;
    int used = cores < (natural_t)capacity ? (int)cores : capacity;
    for (int i = 0; i < used * CPU_STATE_MAX; ++i) ticks[i] = (uint32_t)info[i];
    vm_deallocate(mach_task_self(), (vm_address_t)info, count * sizeof(integer_t));
    return used;
}

int tb_memory(uint64_t *total, uint64_t *available) {
    size_t size = sizeof(*total);
    if (sysctlbyname("hw.memsize", total, &size, NULL, 0) != 0) return 0;
    mach_port_t host = mach_host_self();
    vm_statistics64_data_t vm = {0};
    mach_msg_type_number_t count = HOST_VM_INFO64_COUNT;
    vm_size_t page = 0;
    int ok = host_page_size(host, &page) == KERN_SUCCESS &&
        host_statistics64(host, HOST_VM_INFO64, (host_info64_t)&vm, &count) == KERN_SUCCESS;
    mach_port_deallocate(mach_task_self(), host);
    if (ok) *available = ((uint64_t)vm.free_count + vm.inactive_count) * page;
    return ok;
}

struct tb_disk { uint64_t id, read, written; char name[64]; };

static int counter(CFDictionaryRef dict, CFStringRef key, uint64_t *output) {
    CFTypeRef value = CFDictionaryGetValue(dict, key);
    int64_t n = 0;
    if (!value || CFGetTypeID(value) != CFNumberGetTypeID() ||
        !CFNumberGetValue((CFNumberRef)value, kCFNumberSInt64Type, &n) || n < 0) return 0;
    *output = (uint64_t)n;
    return 1;
}

int tb_disks(struct tb_disk *rows, int capacity) {
    io_iterator_t devices = IO_OBJECT_NULL;
    if (IOServiceGetMatchingServices(kIOMainPortDefault, IOServiceMatching("IOBlockStorageDriver"), &devices)
        != KERN_SUCCESS) return 0;
    io_service_t service;
    int used = 0;
    while (used < capacity && (service = IOIteratorNext(devices)) != IO_OBJECT_NULL) {
        CFTypeRef stats = IORegistryEntryCreateCFProperty(service, CFSTR("Statistics"), kCFAllocatorDefault, 0);
        struct tb_disk row = {0};
        if (stats && CFGetTypeID(stats) == CFDictionaryGetTypeID() &&
            IORegistryEntryGetRegistryEntryID(service, &row.id) == KERN_SUCCESS &&
            counter((CFDictionaryRef)stats, CFSTR("Bytes (Read)"), &row.read) &&
            counter((CFDictionaryRef)stats, CFSTR("Bytes (Write)"), &row.written)) {
            CFTypeRef name = IORegistryEntrySearchCFProperty(service, kIOServicePlane, CFSTR("BSD Name"),
                kCFAllocatorDefault, kIORegistryIterateRecursively);
            if (name && CFGetTypeID(name) == CFStringGetTypeID())
                CFStringGetCString((CFStringRef)name, row.name, sizeof(row.name), kCFStringEncodingUTF8);
            if (name) CFRelease(name);
            rows[used++] = row;
        }
        if (stats) CFRelease(stats);
        IOObjectRelease(service);
    }
    IOObjectRelease(devices);
    return used;
}
