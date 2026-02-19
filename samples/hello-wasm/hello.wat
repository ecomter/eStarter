(module
  ;; ── Imports from eStarter host ──────────────────────────────
  ;; estarter_log(ptr, len) — write a UTF-8 log message
  (import "env" "estarter_log" (func $log (param i32 i32)))
  ;; estarter_api_call(cmdPtr, cmdLen, dataPtr, dataLen) -> i32 status
  (import "env" "estarter_api_call" (func $api_call (param i32 i32 i32 i32) (result i32)))

  ;; ── Memory (1 page = 64 KiB, exported for host access) ─────
  (memory (export "memory") 1)

  ;; ── Data segments — strings stored in linear memory ─────────
  ;;  offset 0: log messages
  (data (i32.const 0)   "[HelloWasm] Starting...")           ;; 22 bytes
  (data (i32.const 32)  "Ping")                              ;;  4 bytes
  (data (i32.const 48)  "[HelloWasm] Ping: OK")              ;; 20 bytes
  (data (i32.const 80)  "[HelloWasm] Ping: FAIL")            ;; 22 bytes
  (data (i32.const 112) "GetTime")                           ;;  7 bytes
  (data (i32.const 128) "[HelloWasm] GetTime: OK")           ;; 23 bytes
  (data (i32.const 160) "[HelloWasm] GetTime: FAIL")         ;; 25 bytes
  (data (i32.const 192) "GetSystemInfo")                     ;; 13 bytes
  (data (i32.const 208) "[HelloWasm] GetSystemInfo: OK")     ;; 29 bytes
  (data (i32.const 240) "[HelloWasm] GetSystemInfo: FAIL")   ;; 31 bytes
  (data (i32.const 272) "[HelloWasm] Done. Exiting.")        ;; 26 bytes

  ;; ── _start (WASI entry point) ───────────────────────────────
  (func $start (export "_start")
    ;; Log: Starting
    (call $log (i32.const 0) (i32.const 22))

    ;; Ping (command at offset 32, len 4, no data)
    (if (i32.eqz (call $api_call (i32.const 32) (i32.const 4) (i32.const 0) (i32.const 0)))
      (then (call $log (i32.const 48) (i32.const 20)))   ;; OK
      (else (call $log (i32.const 80) (i32.const 22)))   ;; FAIL
    )

    ;; GetTime (command at offset 112, len 7, no data)
    (if (i32.eqz (call $api_call (i32.const 112) (i32.const 7) (i32.const 0) (i32.const 0)))
      (then (call $log (i32.const 128) (i32.const 23)))  ;; OK
      (else (call $log (i32.const 160) (i32.const 25)))  ;; FAIL
    )

    ;; GetSystemInfo (command at offset 192, len 13, no data)
    (if (i32.eqz (call $api_call (i32.const 192) (i32.const 13) (i32.const 0) (i32.const 0)))
      (then (call $log (i32.const 208) (i32.const 29)))  ;; OK
      (else (call $log (i32.const 240) (i32.const 31)))  ;; FAIL
    )

    ;; Log: Done
    (call $log (i32.const 272) (i32.const 26))
  )
)
