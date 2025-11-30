<?php
header('Content-Type: text/plain');

echo "=== KIỂM TRA OPENSSL ===\n";
if (extension_loaded('openssl')) {
    echo "[OK] OpenSSL đã được bật.\n";
    echo "Algo SHA256: " . (defined('OPENSSL_ALGO_SHA256') ? "OK" : "Missing") . "\n";
} else {
    echo "[ERROR] OpenSSL CHƯA ĐƯỢC BẬT!\n";
}

echo "\n=== KIỂM TRA FILE KEY ===\n";
$paths = [
    'G:/Study/KLTN/AppQLDVDLLH/app_dllh_may_that/Keys/public_key.pem',
    __DIR__ . '/Keys/public_key.pem'
];
foreach ($paths as $p) {
    echo "Path: $p -> " . (file_exists($p) ? "[FOUND]" : "[MISSING]") . "\n";
}
?>