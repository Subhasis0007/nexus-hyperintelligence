/// WASM Agent 007 — Monitoring capability

#[no_mangle]
pub extern "C" fn agent_init() -> i32 { 0 }

#[no_mangle]
pub extern "C" fn agent_capability() -> *const u8 {
    b"Monitoring\0".as_ptr()
}

#[no_mangle]
pub extern "C" fn agent_execute(input_ptr: *const u8, input_len: u32,
                                 output_ptr: *mut u8, output_capacity: u32) -> i32 {
    if input_ptr.is_null() || output_ptr.is_null() { return -1; }
    let input = unsafe { std::slice::from_raw_parts(input_ptr, input_len as usize) };
    let output = unsafe { std::slice::from_raw_parts_mut(output_ptr, output_capacity as usize) };
    let checksum: u32 = input.iter().map(|&b| b as u32).sum();
    let msg = format!("{{\"agent\":\"007\",\"capability\":\"Monitoring\",\"inputBytes\":{},\"checksum\":{}}}", input.len(), checksum);
    let bytes = msg.as_bytes();
    let write_len = bytes.len().min(output.capacity());
    output[..write_len].copy_from_slice(&bytes[..write_len]);
    write_len as i32
}

#[no_mangle]
pub extern "C" fn agent_version() -> u32 { 1 }
