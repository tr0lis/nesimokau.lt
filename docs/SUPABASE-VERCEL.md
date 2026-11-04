# Supabase ir Vercel paleidimas

## Produkcinė aplinka

1. Supabase projekto **SQL Editor** paleisk visą `database/supabase-schema.sql`. Esamos paskyros ir pažanga paliekamos vietoje; schema prideda serverio autentifikacijos, 2FA, sesijų, ribojimo ir turnyrinės lentelės laikotarpių sumavimo objektus.
2. Vercel projekte pridėk aplinkos kintamuosius iš `.env.example`: `SUPABASE_URL`, `SUPABASE_SERVICE_ROLE_KEY`, `SESSION_SECRET`, `TOTP_ENC_KEY`, `ALLOWED_ORIGINS`, `TOTP_ISSUER`. `SUPABASE_SERVICE_ROLE_KEY` gali būti senasis `service_role` JWT arba naujasis Supabase `sb_secret_` raktas. Naujojo rakto nesiųsk kaip `Authorization: Bearer` — API jį naudoja tik `apikey` antraštėje. Raktą laikyk tik Vercel serveryje, niekada nedėk į naršyklės nustatymus ar `appsettings.json`.
3. `SESSION_SECRET` ir `TOTP_ENC_KEY` sugeneruok atskirai:

   ```powershell
   node -e "console.log(require('crypto').randomBytes(48).toString('base64url'))"
   node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
   ```

4. GitHub repo **Settings → Secrets and variables → Actions** pridėk `VERCEL_TOKEN`, `VERCEL_ORG_ID` ir `VERCEL_PROJECT_ID`. `main` šakos push paleis diegimą.
5. Vercel projekte pridėk `nesimokau.lt` domeną ir DNS pakeisk pagal Vercel pateiktas reikšmes.
6. Supabase **Table Editor → RLS** patikrink, kad anoniminiai ir prisijungę klientai neturėtų tiesioginės prieigos prie duomenų. Schema įjungia RLS ir palieka prieigą tik `service_role`, kurį naudoja Vercel API.

Senoji paskyrų lentelė išsaugoma. Pirmą kartą prisijungus senas PBKDF2 slaptažodžio hash automatiškai pakeičiamas į naują serverio hash. Supabase Auth ir Supabase Storage šiame projekte nenaudojami: autentifikaciją vykdo Vercel API, o avataro duomenys saugomi `user_progress` lentelėje.

## Paleidimas lokaliai

1. Nukopijuok `.env.example` į `.env.local` ir pakeisk pavyzdines / tuščias reikšmes: įrašyk Supabase projekto URL, serverio raktą bei atskiras lokalias `SESSION_SECRET` ir `TOTP_ENC_KEY` reikšmes. `.env.example` pavyzdinis raktas nėra veikiantis prisijungimo raktas.
2. Atskiram kūrimo Supabase projektui naudok jo atskirus URL ir service-role raktą, kad bandymai nepakeistų produkcinių duomenų.
3. PowerShell lange paleisk `scripts\start-local.ps1`. Svetainė pasiekiama adresu `http://localhost:5232`, API – `http://localhost:3011`.
4. 2FA įjungiama prisijungus: **Profilis → Dviejų žingsnių patikra**. QR kodą nuskenuok autentifikavimo programėle ir saugiai išsaugok vienkartinius atsarginius kodus.
