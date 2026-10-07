# Contribuindo

Leia antes de abrir uma issue ou PR, isso agiliza para todo mundo.

English: [CONTRIBUTING.md](CONTRIBUTING.md)

## Abrindo uma issue

Procure issues existentes primeiro. Se nada servir, abra uma com contexto suficiente para agir: o que aconteceu, o que você esperava, como reproduzir (bugs) ou por que é necessário (features).

## Pegando uma issue

Comente pedindo para ser atribuído antes de começar. Espere a confirmação. Pegue só uma issue aberta por vez, termine ou largue antes de pegar outra.

## Enviando um PR

- Referencie a issue que ele fecha (`closes #123`)
- Nunca faça force-push em branch de PR, isso quebra a re-revisão. Envie commits novos, eles são squashados no merge.
- Rode `dotnet test` antes de abrir o PR. A lógica de agenda é a parte que mais quebra em silêncio, então mudanças nela precisam de teste.
- Sem comentários no código. Nomes e funções pequenas carregam a intenção.

## Revisões

Seja direto, não duro. Use comentários inline para sugestões específicas. Se o feedback já foi dado, não repita, um joinha basta para concordar.
