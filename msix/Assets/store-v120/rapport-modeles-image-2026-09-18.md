# Rapport — modèles de génération d'images pour visuels Microsoft Store (état au 2026-09-18)

Aucun verdict ci-dessous : faits ancrés uniquement. Beaucoup de résultats de recherche pointent vers des agrégateurs tiers (pricepertoken.com, checkthat.ai, costbench.com, felloai.com, etc.) — signalés explicitement comme **sources faibles**, distinctes des pages éditeur officielles.

## 1. État des modèles (noms exacts, sources officielles)

- **OpenAI** — `gpt-image-1` : "a natively multimodal language model that accepts both text and image inputs, and produces image outputs" — page modèle officielle : https://developers.openai.com/api/docs/models/gpt-image-1 (page consultée 2026-09-18, marqué comme "previous image generation model").
- **OpenAI** — `gpt-image-2` : existe, page officielle https://developers.openai.com/api/docs/models/gpt-image-2 (confirmée par recherche, contenu non entièrement re-fetché en direct — voir section 2).
- **OpenAI** — `gpt-image-2.5-flare` : "our fastest model for high-quality, everyday image generation" — https://developers.openai.com/api/docs/models/gpt-image-2.5-flare — snapshot par défaut `gpt-image-2.5-flare-2026-09-08` (donc annonce datée du 2026-09-08).
- **OpenAI** — `gpt-image-2.5-sunburst` : variante "premium visual workflows" — https://developers.openai.com/api/docs/models/gpt-image-2.5-sunburst.
- **OpenAI** — annonce produit : "Introducing ChatGPT Images 2.5" — https://openai.com/index/introducing-chatgpt-images-2-5/ (titre trouvé via recherche ; le fetch direct a été refusé en 403, donc le contenu détaillé de cette page n'a **pas** été vérifié en direct, seul le titre/snippet est confirmé).
- **Google** — "Nano Banana" est le surnom public de **Gemini 2.5 Flash Image** ; il existe aussi "Nano Banana Pro" (backbone Gemini 3 Pro). Ligne produit distincte : **Imagen 4** (Imagen 4 / Imagen 4 Fast / Imagen 4 Ultra), "dedicated text-to-image line, separate from the Nano Banana / Gemini Image conversational line" — sources agrégées (aifreeapi.com, pricepertoken.com) ; **pas de fetch direct de la page officielle Google DeepMind/ai.google.dev effectué pour ce point** — à vérifier avant usage si le nom exact compte.
- **Black Forest Labs (FLUX)** — génération **FLUX.2** (variantes `[pro]`, `[flex]`, `[dev]`, `[klein]` ajoutée janvier 2026), introduite novembre 2025 selon innfactory.ai (source tierce). Page tarifs officielle : https://bfl.ai/pricing (non re-fetchée directement — 403/non testée). FLUX 3 Video mentionné séparément (source tierce futureagi.com).
- **Midjourney** — V7 (sortie 2025-04-03, par défaut du 2025-06-17 au 2026-06-09), puis **V8.1** (sortie 2026-04-14, par défaut à partir du 2026-06-10) — source citée : docs.midjourney.com (page "Version"), mais citation reprise via résumé de recherche, pas de fetch direct effectué.
- **Adobe Firefly** — "Image Model 5" évoqué comme dernier modèle (octobre 2025), après "Firefly Image 4" et "Image 4 Ultra" (avril 2025) — source tierce (Wikipedia repris par la recherche). **Le fetch direct de firefly.adobe.com/pricing a échoué** (page rendue en JS, aucun contenu utile récupéré) : nom exact et tarifs Adobe **non confirmés en direct**.
- **Ideogram** — modèle "Ideogram 3.0" (Turbo / Default / Quality) — fetch direct de ideogram.ai/pricing refusé (403). Sources tierces seulement (pricepertoken.com, eesel.ai).
- **Recraft** — confirmé via fetch direct de recraft.ai/pricing : "All models across the V2, V3, V4 and V4.1 generations are available via the API" (citation directe de la FAQ de la page officielle). Donc Recraft V4.1 est la génération la plus récente listée sur la page officielle au 2026-09-18.
- **Stability AI** — famille "Stable Diffusion 3.5" (Large, Large Turbo, Medium, Flash) + "Stable Image Ultra/Core" — uniquement sources tierces (pricepertoken, cloudprice, developer.puter.com) ; pas de fetch direct de platform.stability.ai effectué.

## 2. Vérification « GPT Image 2.5 »

**Le modèle existe bel et bien**, contrairement à l'hypothèse implicite de la question. Confirmé par fetch direct de la page officielle développeur :

- Page officielle : https://developers.openai.com/api/docs/models/gpt-image-2.5-flare — "our fastest model for high-quality, everyday image generation. It delivers higher-quality images than GPT-Image-2 at 50% lower latency."
- Snapshot daté : `gpt-image-2.5-flare-2026-09-08` → **annonce/déploiement le 2026-09-08**.
- Variante haut de gamme : `gpt-image-2.5-sunburst` — https://developers.openai.com/api/docs/models/gpt-image-2.5-sunburst — "built for premium visual workflows".
- Disponibilité : "available to all ChatGPT, ChatGPT Work, and Codex users across desktop, mobile, and web" (résumé de recherche du titre de page officiel openai.com/index/introducing-chatgpt-images-2-5/, **contenu de cette page non vérifié en direct** — 403 au fetch).
- Chaîne de versions confirmée dans l'ordre : `gpt-image-1` (marqué "previous" sur sa propre fiche officielle) → `gpt-image-2` (lancé, selon community.openai.com, sous le titre "Introducing gpt-image-2 - available today in the API and Codex") → `gpt-image-2.5` (Flare/Sunburst, 2026-09-08).
- Tarifs `gpt-image-2.5-flare` lus en direct sur la page officielle : text input $5/1M tokens, image input $8/1M tokens, image output $30/1M tokens, cache -75%.

**Point de prudence méthodo** : la première recherche brute avait renvoyé des pages d'agrégateurs (weshop.ai, morphic.com) donnant ce nom — j'ai vérifié indépendamment sur les pages `developers.openai.com` officielles avant de le confirmer, conformément à la consigne. Le nom est donc confirmé par une source primaire, pas seulement par un tiers.

## 3. Rendu du texte / diacritiques français

**Rien de spécifique aux accents français n'a été trouvé.** Recherche de benchmarks :
- "STRICT: Stress-Test of Rendering Image Containing Text" (ACL Anthology, EMNLP 2025) évalue le rendu de texte multilingue par OCR (NED, CER, WER) — mention générale du français comme langue couverte, mais **aucune citation littérale trouvée sur les diacritiques (é, ç, «, »)** spécifiquement. https://aclanthology.org/2025.emnlp-main.1070.pdf
- Autres benchmarks cités par la recherche (CVTG-2K, LongText-Bench, TextAtlasEval, GlyphAcc-Multilingual) : simples titres/mentions, **contenu non vérifié par fetch direct**.
- **Non trouvé** : aucune source (officielle ou tierce) documentant explicitement un succès ou un échec sur "é / è / à / ç / É / Ç / « »" pour un modèle nommé (GPT Image 2.5, Nano Banana, FLUX.2, Ideogram 3.0, etc.). C'est un vrai trou de recherche, pas une absence de problème.

## 4. Interfaces / captures d'écran logicielles crédibles

**Rien trouvé de spécifique.** Aucune source (benchmark, page éditeur, article technique) documentant explicitement la capacité ou l'échec des modèles ci-dessus à générer des maquettes Windows/claviers virtuels sans touches dupliquées ou lettres inventées. Angle cherché : "image generation model benchmark French diacritics text rendering" (a remonté des benchmarks texte génériques, pas UI) — pas de requête UI-spécifique menée par manque de résultats pertinents en amont. **À rechercher séparément si ce point est bloquant.**

## 5. Règles Microsoft Store

Fetch direct de la page officielle (version 7.20, `ms.date: 2026-09-14`, mise à jour `2026-09-15`) : https://learn.microsoft.com/en-us/windows/apps/publish/store-policies

- **Clause 10.1 "Distinct Function & Value; Accurate Representation"** : "Your product and its associated metadata, including but not limited to your app title, description, screenshots, trailers, content rating, search terms, and product category, must accurately and clearly reflect the source, functionality, and features of your product."
- **Clause 10.1.1** : "All aspects of your product, including metadata, should accurately describe the functions, features, user experience and any important limitations of your product... Your product must not in any way attempt to mislead customers as to its actual features, functionality, or relationship to other products."
- **Clause 11.16 "Live Generative AI Content"** (existe, mais porte sur un cas différent) : "Products that contain dynamic content created by generative AI models in response to user inputs must: Disclose the use of live generative AI in the metadata. Note the use of live generative AI in Partner Center during the submission process..." — **Cette clause vise les apps qui embarquent elles-mêmes une IA générative en fonctionnement (contenu dynamique créé en réponse aux entrées utilisateur), pas les visuels marketing/screenshots de la fiche Store fabriqués en amont avec un outil IA.** Aucune clause distincte trouvée imposant une divulgation "généré par IA" pour les images de la fiche Store elles-mêmes.
- Page technique screenshots (dernière mise à jour 2026-08-24) : https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images — précise les formats (1920×1080 accepté pour l'image "16:9 Super hero art" et les vignettes de trailer) mais **ne dit pas explicitement** que les screenshots doivent être des captures brutes de l'app ; recommande cependant : "Showcase the app's key features and UI" (FAQ de la page), ce qui suppose une fidélité fonctionnelle, cohérent avec la clause 10.1.
- **Non trouvé** : un texte Microsoft imposant un étiquetage "AI-generated" spécifique aux images de fiche produit (screenshots/product images), distinct de la clause 11.16 sur le contenu généré dynamiquement dans l'app.

## 6. C2PA / SynthID

- **OpenAI** : page d'aide officielle référencée (non re-fetchable, 403) : https://help.openai.com/en/articles/8912793-c2pa-in-chatgpt-images ("Provenance signals (Content Credentials, SynthID) in OpenAI-generated content"). Contenu résumé par la recherche (non vérifié en direct) : "OpenAI began adding C2PA metadata to all images created and edited by DALL·E 3 in ChatGPT and the OpenAI API" — daté 2024, **pas confirmé si cela s'applique nommément à gpt-image-1/2/2.5**.
- **Google** : SynthID "already integrated into Google's generative AI products: Gemini for text, Imagen for images, Lyria for audio, and Veo for video" — page officielle DeepMind : https://deepmind.google/models/synthid/ (titre confirmé, contenu détaillé non re-fetché en direct).
- **Survie du filigrane après upload sur un store** : **aucune source trouvée**, ni dans un sens ni dans l'autre. Ni OpenAI ni Google ne documentent explicitement la persistance de C2PA/SynthID après compression/upload sur Microsoft Partner Center. Point non traité par les sources disponibles — à considérer comme un vrai "non trouvé", pas une absence de risque.
- Black Forest Labs, Midjourney, Adobe Firefly, Ideogram, Recraft, Stability : **aucune source consultée sur leur politique de watermarking** dans cette recherche (angle non couvert par manque de temps de recherche — à traiter si nécessaire).

## 7. Tarifs (résumé, fiabilité variable)

| Modèle | Prix trouvé | Source | Fiabilité |
|---|---|---|---|
| gpt-image-2.5-flare (OpenAI) | text in $5/1M tok, image in $8/1M tok, image out $30/1M tok | https://developers.openai.com/api/docs/models/gpt-image-2.5-flare | **Officiel, fetch direct** |
| gpt-image-1 (OpenAI) | "$5.00 per 1M text input tokens to $40.00 per 1M image output tokens" (≈$0.02/$0.07/$0.19 par image low/med/high) | résumé de recherche, page source non identifiée précisément | Tierce, à revérifier |
| Recraft (plans) | Plans "mensuel/annuel (-20%)" mentionnés, montants précis non extraits | https://www.recraft.ai/pricing (fetch direct, mais chiffres non chargés dans le contenu récupéré) | Officiel mais **incomplet** |
| Imagen 4 Fast / Ultra | $0.02 / $0.06 par image | pricepertoken.com (tiers) | **Faible, non officiel** |
| Nano Banana Pro | $0.134 (1K/2K), $0.24 (4K) | pricepertoken.com / aifreeapi.com (tiers) | **Faible, non officiel** |
| FLUX Schnell/Dev/Pro/Ultra | $0.0005 à $0.20/image | futureagi.com, pricepertoken.com (tiers) | **Faible, non officiel** — page officielle bfl.ai/pricing non fetchée |
| Ideogram 3.0 (Turbo/Default/Quality) | $0.03 / $0.06 / $0.09 (API) | pricepertoken.com (tiers) | **Faible, non officiel** — fetch direct ideogram.ai/pricing refusé (403) |
| Stable Diffusion 3.5 (Large/Medium/Flash/Turbo) | $0.025 à $0.065/image | developer.puter.com, pricepertoken.com (tiers) | **Faible, non officiel** |
| Adobe Firefly (plans) | $9.99 à $199.99/mois | costbench.com, saascrmreview.com (tiers) | **Faible, non officiel** — firefly.adobe.com/pricing fetché mais rendu JS, chiffres non récupérés |

## Contradictions et sources faibles à noter explicitement

- La quasi-totalité des chiffres tarifaires pour Google, FLUX, Ideogram, Stability et Adobe provient d'agrégateurs tiers (pricepertoken.com, felloai.com, costbench.com, etc.), jamais confirmés par fetch direct de la page éditeur dans cette session (échecs 403 ou rendu JS). **Ne pas les citer comme définitifs.**
- Aucune contradiction factuelle directe relevée entre sources sur l'existence de GPT Image 2.5 — au contraire, toutes convergent (community.openai.com, developers.openai.com, openai.com).

## Non trouvé (à rechercher si nécessaire)

1. Citation littérale documentant le rendu ou l'échec des diacritiques français (é, è, à, ç, «, ») pour un modèle nommé.
2. Toute source sur la génération d'interfaces Windows/claviers virtuels crédibles (touches dupliquées, géométrie).
3. Contenu détaillé de la page officielle OpenAI C2PA (fetch 403) — seul le titre est confirmé.
4. Persistance de C2PA/SynthID après upload/compression sur un store (Microsoft ou autre) — angle non documenté nulle part.
5. Politique de watermarking de Black Forest Labs, Midjourney, Adobe Firefly, Ideogram, Recraft, Stability — non recherchée dans cette session.
6. Nom exact et tarifs officiels Adobe Firefly (page JS non exploitable), Ideogram (403), FLUX (bfl.ai/pricing non fetché), Google Imagen/Nano Banana (page ai.google.dev non fetchée directement).
