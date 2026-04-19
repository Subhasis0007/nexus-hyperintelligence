/// WASM Agent 001 — Analytics capability
/// Compiles to wasm32-unknown-unknown

#[no_mangle]
pub extern "C" fn agent_init() -> i32 {
    0 // success
}

#[no_mangle]
pub extern "C" fn agent_capability() -> *const u8 {
    b"Analytics\0".as_ptr()
}

/// Execute an analytics task on the provided data buffer.
/// Returns the number of bytes written to the output.
#[no_mangle]
pub extern "C" fn agent_execute(input_ptr: *const u8, input_len: u32,
                                 output_ptr: *mut u8, output_capacity: u32) -> i32 {
    if input_ptr.is_null() || output_ptr.is_null() {
        return -1;
    }
    // Safety: pointers come from the WASM host which allocates them correctly
    let input = unsafe { std::slice::from_raw_parts(input_ptr, input_len as usize) };
    let output = unsafe { std::slice::from_raw_parts_mut(output_ptr, output_capacity as usize) };

    // Simple analytics: compute checksum and byte frequency
    let checksum: u32 = input.iter().map(|&b| b as u32).sum();
    let msg = format!("{{\"agent\":\"001\",\"capability\":\"Analytics\",\"inputBytes\":{},\"checksum\":{}}}", input.len(), checksum);
    let bytes = msg.as_bytes();
    let write_len = bytes.len().min(output.capacity());
    output[..write_len].copy_from_slice(&bytes[..write_len]);
    write_len as i32
}

#[no_mangle]
pub extern "C" fn agent_version() -> u32 {
    1
}
