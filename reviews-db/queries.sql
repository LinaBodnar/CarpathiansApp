db = db.getSiblingDB('ReviewContext');

print('\n--- 1. Відгуки маршруту 1, найновіші першими (ix_reviews_route_created) ---');
printjson(db.reviews.find({ routeId: 1 }, { author: 1, rating: 1, createdAt: 1 }).sort({ createdAt: -1 }).toArray());

print('\n--- 2. Гнучка схема: документи з різними наборами полів ---');
print('є photos: '   + db.reviews.countDocuments({ photos:   { $exists: true } }));
print('є gpsTrack: ' + db.reviews.countDocuments({ gpsTrack: { $exists: true } }));
print('є weather: '  + db.reviews.countDocuments({ weather:  { $exists: true } }));
print('є comments: ' + db.reviews.countDocuments({ comments: { $exists: true } }));
printjson(db.reviews.aggregate([{ $project: { _id: 0, routeId: 1, fields: { $map: { input: { $objectToArray: '$$ROOT' }, as: 'f', in: '$$f.k' } } } }]).toArray());

print('\n--- 3. Відгуки перевірених учасників ---');
printjson(db.reviews.find({ isVerifiedParticipant: true }, { routeName: 1, rating: 1, 'author.displayName': 1 }).toArray());

print('\n--- 4. Повнотекстовий пошук (tx_reviews_text) ---');
printjson(db.reviews.find({ $text: { $search: 'озеро' } }, { routeName: 1, text: 1 }).toArray());

print('\n--- 5. Рейтинг по маршрутах (середня оцінка і кількість), найкращі першими ---');
printjson(db.reviews.aggregate([
  { $group: { _id: { routeId: '$routeId', routeName: '$routeName' }, avgRating: { $avg: '$rating' }, reviews: { $sum: 1 } } },
  { $project: { _id: 0, routeId: '$_id.routeId', routeName: '$_id.routeName', avgRating: { $round: ['$avgRating', 2] }, reviews: 1 } },
  { $sort: { avgRating: -1, reviews: -1 } }
]).toArray());

print('\n--- 6. Вбудовані коментарі: кількість коментарів на відгук ($size) ---');
printjson(db.reviews.aggregate([
  { $match: { comments: { $exists: true } } },
  { $project: { _id: 0, routeName: 1, author: '$author.displayName', commentsCount: { $size: '$comments' } } }
]).toArray());

print('\n--- 7. Referencing: відгуки з обговоренням ($lookup на discussions) ---');
printjson(db.reviews.aggregate([
  { $match: { discussionId: { $exists: true } } },
  { $lookup: { from: 'discussions', localField: 'discussionId', foreignField: '_id', as: 'discussion' } },
  { $unwind: '$discussion' },
  { $project: { _id: 0, routeName: 1, rating: 1, discussionTitle: '$discussion.title', messageCount: '$discussion.messageCount' } }
]).toArray());

print('\n--- 8. Гібрид: коротке читання з lastMessages, без звернення до discussionMessages ---');
printjson(db.discussions.find({ routeId: 1 }, { title: 1, messageCount: 1, lastMessages: 1 }).toArray());

print('\n--- 9. Гібрид: повна гілка з пагінацією (ix_messages_discussion_created) ---');
printjson(db.discussionMessages.find({ discussionId: ObjectId('66f0c3000000000000000001') })
  .sort({ createdAt: 1 }).skip(0).limit(10).toArray());

print('\n--- 10. Активність користувачів: відгуки та їх сума оцінок ---');
printjson(db.reviews.aggregate([
  { $group: { _id: '$author.userId', name: { $first: '$author.displayName' }, reviews: { $sum: 1 }, avgRating: { $avg: '$rating' } } },
  { $sort: { reviews: -1 } }
]).toArray());

print('\n--- 11. Зведена колекція ratings ---');
printjson(db.ratings.find({}, { _id: 0 }).sort({ routeId: 1 }).toArray());

print('\n--- 12. Перевірка JSON Schema: відгук з оцінкою 7 має бути відхилений ---');
try {
  db.reviews.insertOne({
    routeId: NumberInt(1), routeName: 'x', author: { userId: '9', displayName: 'Тест' },
    rating: NumberInt(7), text: 'не пройде', createdAt: new Date()
  });
  print('ПРОВАЛ: документ із rating 7 вставлено');
} catch (e) {
  print('OK: ' + e.codeName);
}
