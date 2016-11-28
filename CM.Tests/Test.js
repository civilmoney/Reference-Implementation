// These tests run using the Chutzpah visual studio test runner add-in.
// https://github.com/mmanela/chutzpah/wiki/Running-JavaScript-tests-with-Chutzpah

/// <reference path="../CM.Javascript/www/webworkers.js" />
/// <reference path="../CM.Javascript/www/CM.js" />
test("javascript s_OK should be 0", function () {
    equal(CM.CMResult.s_OK.code, 0, "should add 0");
});

test("javascript rfc2898 (1)", function () {
    var rfc = CM.Cryptography.Rfc2898.createHMACSHA1(
         System.Convert.fromBase64String("2C+N7tv+ICSXdgZPl5OxIrKIWMLf271djN+8WNWXkSo="),
         System.Convert.fromBase64String("zeX/4i4rmNpxtw=="),
         10000
         );
    var key = rfc.getBytes(32);
    var iv = rfc.getBytes(16);

    equal(System.Convert.toBase64String(key), "fmVu92Y9Pzq2PFODcprjjSM9a6w6H1UST4MJ9nb5tpU=", "key match 1");
    equal(System.Convert.toBase64String(iv), "WltBEhFECAavyuiTkaNl8A==", "iv match 1");

})

test("javascript rfc2898 (2)", function () {
    var rfc = CM.Cryptography.Rfc2898.createHMACSHA1(
         System.Convert.fromBase64String("CSqaCH7Ncm6uOVUQZQFzjtMdcQCwCz2R4ZxliS6jbuY="),
         System.Convert.fromBase64String("vU0ESqzvlswk83B4m2bMxQnGxKxDNc2RyPeE7TiXu95THOQAAXHM"),
         10000
         );
    var key = rfc.getBytes(32);
    var iv = rfc.getBytes(16);

    equal(System.Convert.toBase64String(key), "bRz+T0fMZxbW3uXr7iU3rglWCHP5CXeN268kllaD7IQ=", "key match 2");
    equal(System.Convert.toBase64String(iv), "eQ1rQdFUYJ54w3FU3Btvqw==", "iv match 2");

})

// AES Fixes for bridge performance bugs:
// addRoundKey;
//  - remove ending "& 255"
//  - remove "| 0"
//  - remove " >>> 0"

test("javascript AES (1)", function () {
   
    var enc = CM.Cryptography.AES.encrypt(
        System.Text.Encoding.UTF8.getBytes("hello"),
        System.Convert.fromBase64String("bRz+T0fMZxbW3uXr7iU3rglWCHP5CXeN268kllaD7IQ="),
        System.Convert.fromBase64String("eQ1rQdFUYJ54w3FU3Btvqw=="));

    equal(System.Convert.toBase64String(enc), "QpkQOmjYhSjYb2z3yVk0+g==", "5 byte match");
})

test("javascript AES (2)", function () {

    var enc = CM.Cryptography.AES.encrypt(
        System.Text.Encoding.UTF8.getBytes("something16bytes"),
        System.Convert.fromBase64String("bRz+T0fMZxbW3uXr7iU3rglWCHP5CXeN268kllaD7IQ="),
        System.Convert.fromBase64String("eQ1rQdFUYJ54w3FU3Btvqw=="));

    equal(System.Convert.toBase64String(enc), "8ySrsO2agJiwM8rF3aln6u6rMgAqP6As0mI52iqY9no=", "16 byte match");
})

// test("javascript RSA (1)", function () {
//     var priv = System.Convert.fromBase64String("Auji8HaEUassO9YsAKVhs0ExGvK74BIXiIdXC04qWDe5Z7AbW4V8Sj/3ysKSORA1ayF54aCFAAtr6AiLEK41cR+n6MzbWeuorjqv1MvO8Eg62gQMsPNbwFnnPf9iWC4AT2V7wN9akOiYBtB1V7Tklw3StTNTPIAuePRR36vBzUk=");
//     var pub = System.Convert.fromBase64String("B+hzmpvwi3MUOVpta24HxyCuPDausCpgNgDjIXQ5DkTaWTwkN/pOxQpG5ylvXEfrZcFq2oH4fVxsqXt/8E0ubqyzltgWHDuG7stNsaEosExvZQfH2ehmXGbX3k3GnT1hbxOsk4X60JxCMJVkp9QUzP2oP6hOf8OmFQLgbShBlKE=");
//     var data = System.Convert.fromBase64String("YWFhYjIwMTYtMDYtMDlUMTc6MDQ6MDEyMDE2LTA2LTA5VDE3OjA0OjAxMUFGLUJERzAsYzlBSFlVQzNpRFBTd050eE0yYW55YUNtcmNjV255YnQ4cjhldXJlVnMvVT0sMkJUb2tDWU5raUtLcTlSSGRTbjNvZ1RkcjVNemh3Wm84YVQ0UzA3MXBpL2EzMHB1ZkY0NnpmOUVUenBoaUQ1WmphZWtIQjJxTWJzbkFjdlEvYlNwZDRON3k3YnQ1NmJ0aUFZNUZCeEt3WlJ2RUNCZURzaFQ2RmlPelc3ODdiemQ4bjlqeDZJYk5FMzN2NWQwdllodlRmTFV1VlpVbWpMejRKd0J2Wjk5QVByMWpxR2twUXo1R2ZVanVQTHAyYkNxMjAxNi0wNi0wOVQxNzowNDowMSxCK2h6bXB2d2kzTVVPVnB0YTI0SHh5Q3VQRGF1c0NwZ05nRGpJWFE1RGtUYVdUd2tOL3BPeFFwRzV5bHZYRWZyWmNGcTJvSDRmVnhzcVh0LzhFMHVicXl6bHRnV0hEdUc3c3ROc2FFb3NFeHZaUWZIMmVobVhHYlgzazNHblQxaGJ4T3NrNFg2MEp4Q01KVmtwOVFVelAyb1A2aE9mOE9tRlFMZ2JTaEJsS0U9LA==");
//     var hashed = CM.Cryptography.SHA256.computeHash(data);
//     var sig = CM.Cryptography.RSA.signData256(priv, pub, hashed);
//     equal(System.Convert.toBase64String(sig), "BE4NVtMprx0CccJEjHzL8LxnLnJH6iRAz6NasSLdneKsnWhOBLUJ2nD+UZBGX4w3bWreNZOVDu7PN8lgP42Zu2rFvqjCcK/VdWxy8Hzdkgpcwf5d8bwwQ/DmfpiXgqkLG+3VPKPgEGQi44Zk76GgS9M8t4FusPn6uOF6rf0n8Ws=", "signature match");
// });
test("javascript RSA (2)", function () {
    var priv = System.Convert.fromBase64String("Auji8HaEUassO9YsAKVhs0ExGvK74BIXiIdXC04qWDe5Z7AbW4V8Sj/3ysKSORA1ayF54aCFAAtr6AiLEK41cR+n6MzbWeuorjqv1MvO8Eg62gQMsPNbwFnnPf9iWC4AT2V7wN9akOiYBtB1V7Tklw3StTNTPIAuePRR36vBzUk=");
    var pub = System.Convert.fromBase64String("B+hzmpvwi3MUOVpta24HxyCuPDausCpgNgDjIXQ5DkTaWTwkN/pOxQpG5ylvXEfrZcFq2oH4fVxsqXt/8E0ubqyzltgWHDuG7stNsaEosExvZQfH2ehmXGbX3k3GnT1hbxOsk4X60JxCMJVkp9QUzP2oP6hOf8OmFQLgbShBlKE=");
    var data = System.Convert.fromBase64String("YWFhYjIwMTYtMDYtMDlUMTc6MDQ6MDEyMDE2LTA2LTA5VDE3OjA0OjAxMUFGLUJERzAsYzlBSFlVQzNpRFBTd050eE0yYW55YUNtcmNjV255YnQ4cjhldXJlVnMvVT0sMkJUb2tDWU5raUtLcTlSSGRTbjNvZ1RkcjVNemh3Wm84YVQ0UzA3MXBpL2EzMHB1ZkY0NnpmOUVUenBoaUQ1WmphZWtIQjJxTWJzbkFjdlEvYlNwZDRON3k3YnQ1NmJ0aUFZNUZCeEt3WlJ2RUNCZURzaFQ2RmlPelc3ODdiemQ4bjlqeDZJYk5FMzN2NWQwdllodlRmTFV1VlpVbWpMejRKd0J2Wjk5QVByMWpxR2twUXo1R2ZVanVQTHAyYkNxMjAxNi0wNi0wOVQxNzowNDowMSxCK2h6bXB2d2kzTVVPVnB0YTI0SHh5Q3VQRGF1c0NwZ05nRGpJWFE1RGtUYVdUd2tOL3BPeFFwRzV5bHZYRWZyWmNGcTJvSDRmVnhzcVh0LzhFMHVicXl6bHRnV0hEdUc3c3ROc2FFb3NFeHZaUWZIMmVobVhHYlgzazNHblQxaGJ4T3NrNFg2MEp4Q01KVmtwOVFVelAyb1A2aE9mOE9tRlFMZ2JTaEJsS0U9LA==");
    var hashed = CM.Cryptography.SHA256.computeHash(data);
    //var sig = CM.Cryptography.RSA.signData256(priv, pub, hashed);
    var c = CM.Cryptography.RSA.eMSA_PKCS1_v1_5Encode_256(hashed, priv.length);
    var crunch = new Crunch();
    var sig = crunch.exp(c, priv, pub)
    equal(System.Convert.toBase64String(sig), "BE4NVtMprx0CccJEjHzL8LxnLnJH6iRAz6NasSLdneKsnWhOBLUJ2nD+UZBGX4w3bWreNZOVDu7PN8lgP42Zu2rFvqjCcK/VdWxy8Hzdkgpcwf5d8bwwQ/DmfpiXgqkLG+3VPKPgEGQi44Zk76GgS9M8t4FusPn6uOF6rf0n8Ws=", "signature match");
});

test("javascript RSA (3)", function () {
    var priv = System.Convert.fromBase64String("Ss/34he8NdboyCoW/K9Gvqv4RhCM6znc06AVoyM+jbg+CgUQmR78XvFqNwCWIJ4VGbHdPFpMjpRNLm5tNK+2Z700vJbpAGRFSxdhz938GuGSewzPNcnWbluFgGpYoIk8M1hFhFWVIDM9MaBBIL3U8X4UYKL58tkRm317xHFTsgE=");
    var pub = System.Convert.fromBase64String("dLiNyruff8skKIhpQSNhYz66nzba9QKxrAyvGT+M2MHS5B5wZLmkvhT+/aomQW4vN8HUuBiHfs38+C9EBRm5k1vSBnhKjNmJInsydfWMWaklKE6yC2P3imJCBnBzSIGx63PIGHQ9x8n+k1pKQnU1xSciB9/PFuyWEYOyyE5Kxyk=");
    var data = System.Convert.fromBase64String("MjAxNi0wOS0wOVQyMzoyMDoxMjEuMDAwMDAwdGVzdDF0ZXN0NDIwMTYtMDktMDlUMjM6MjA6MTIBQ0EtTlM=");
    var hashed = CM.Cryptography.SHA256.computeHash(data);
    equal(System.Convert.toBase64String(hashed), "oTOkST3WsEzJpqUu7AKDmhZN55JSkY8E41Jj7xPTjOU=", "SHA256 match");
    //var sig = CM.Cryptography.RSA.signData256(priv, pub, hashed);
    var c = CM.Cryptography.RSA.eMSA_PKCS1_v1_5Encode_256(hashed, priv.length);
    equal(System.Convert.toBase64String(c), "AAH//////////////////////////////////////////////////////////////////////////////////////////////////wAwMTANBglghkgBZQMEAgEFAAQgoTOkST3WsEzJpqUu7AKDmhZN55JSkY8E41Jj7xPTjOU=", "PKCS1 match");

    var crunch = new Crunch();
    var sig = crunch.exp(c, priv, pub)
    while (sig.length < priv.length)
        sig.splice(0, 0, 0);
    equal(System.Convert.toBase64String(sig), "AH6wP/k7uatpaiR2c70PdzC+BeYbj0LTtoBDy91FJsahfwJo8lG+5tawuFcosRpFH+t/ZJzVLNmB9lJhYzVO/uB1c+HBzq/Crgt8xgO/XU+guuf4iy4TxzHqPrrHndWg9A3P3oa25tBqfNn8z+cKG8Lw3VzzqYgee+7FeI0NGRs=", "signature match");
});


test("javascript MD5 (1)", function () {
    var b = System.Convert.fromBase64String("AAECAwQFBgcICQ==");
    var sig = CM.Cryptography.MD5.computeHash(b);
    equal(System.Convert.toBase64String(sig), "xWvVSA9uVBPLYqCtlmZhOg==", "signature match");
});

