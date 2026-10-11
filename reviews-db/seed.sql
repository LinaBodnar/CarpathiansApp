db = db.getSiblingDB('ReviewContext');

function upsert(collection, doc) {
  const { _id, ...rest } = doc;
  db.getCollection(collection).updateOne({ _id: _id }, { $setOnInsert: rest }, { upsert: true });
}

const olena  = { userId: '1', displayName: 'Олена' };
const andrii = { userId: '2', displayName: 'Андрій' };
const maria  = { userId: '3', displayName: 'Марія' };

const R1 = 'Заросляк – Говерла';
const R2 = 'Заросляк – Несамовите – Говерла';
const R3 = 'Заросляк – Говерла – Петрос';

const D1 = ObjectId('66f0c3000000000000000001');
const D2 = ObjectId('66f0c3000000000000000002');


// 1) Повний відгук: photos + comments + discussionId + isVerifiedParticipant
upsert('reviews', {
  _id: ObjectId('66f0c2000000000000000001'),
  routeId: NumberInt(1), routeName: R1,
  author: olena,
  rating: NumberInt(5),
  text: 'Чудовий маршрут, з вершини відкривається неймовірна панорама.',
  photos: ['hoverla-top-1.jpg', 'hoverla-top-2.jpg'],
  comments: [
    { author: andrii, text: 'Згоден, краєвиди чудові', createdAt: ISODate('2026-09-02T09:30:00Z') }
  ],
  discussionId: D1,
  isVerifiedParticipant: true,
  createdAt: ISODate('2026-09-01T10:00:00Z')
});

// 2) Мінімальний відгук + погода (без photos, comments, discussionId)
upsert('reviews', {
  _id: ObjectId('66f0c2000000000000000002'),
  routeId: NumberInt(1), routeName: R1,
  author: andrii,
  rating: NumberInt(4),
  text: 'Гарний підйом, але на вершині сильний вітер.',
  weather: { tempC: 8, wind: 'strong', conditions: 'cloudy' },
  isVerifiedParticipant: false,
  createdAt: ISODate('2026-09-05T12:15:00Z')
});

// 3) GPS-трек + photos + tags
upsert('reviews', {
  _id: ObjectId('66f0c2000000000000000003'),
  routeId: NumberInt(2), routeName: R2,
  author: maria,
  rating: NumberInt(5),
  text: 'Озеро Несамовите варте окремого заходу.',
  photos: ['nesamovyte-1.jpg'],
  gpsTrack: [
    { lat: 48.1660, lng: 24.5460, ele: 1050 },
    { lat: 48.1550, lng: 24.5100, ele: 1750 },
    { lat: 48.1601, lng: 24.5004, ele: 2061 }
  ],
  tags: ['lake', 'photography'],
  discussionId: D2,
  createdAt: ISODate('2026-09-10T16:40:00Z')
});

// 4) Відгук із коментарями та тегами, без фото
upsert('reviews', {
  _id: ObjectId('66f0c2000000000000000004'),
  routeId: NumberInt(3), routeName: R3,
  author: olena,
  rating: NumberInt(3),
  text: 'Дуже довгий день, взяла замало води.',
  tags: ['long', 'water'],
  comments: [
    { author: maria, text: 'Так, води потрібно щонайменше 2 літри', createdAt: ISODate('2026-09-12T08:00:00Z') },
    { author: andrii, text: 'Дякую за пораду для наступного разу', createdAt: ISODate('2026-09-12T09:10:00Z') }
  ],
  createdAt: ISODate('2026-09-11T20:05:00Z')
});

// 5) Інший користувач, інший маршрут: проста оцінка з погодою
upsert('reviews', {
  _id: ObjectId('66f0c2000000000000000005'),
  routeId: NumberInt(2), routeName: R2,
  author: andrii,
  rating: NumberInt(4),
  text: 'Стежка до озера місцями слизька після дощу.',
  weather: { tempC: 11, wind: 'light', conditions: 'rain' },
  createdAt: ISODate('2026-09-14T11:20:00Z')
});

const m1 = { author: olena,  text: 'Чи потрібні палиці на Говерлу?',                         createdAt: ISODate('2026-09-03T08:00:00Z') };
const m2 = { author: andrii, text: 'Для спуску дуже допомагають.',                           createdAt: ISODate('2026-09-03T08:20:00Z') };
const m3 = { author: maria,  text: 'Я ходила без них, але з палицями було б легше.',         createdAt: ISODate('2026-09-03T09:05:00Z') };
const m4 = { author: olena,  text: 'Дякую, візьму.',                                         createdAt: ISODate('2026-09-03T09:30:00Z') };

upsert('discussions', {
  _id: D1,
  routeId: NumberInt(1), routeName: R1,
  title: 'Чи потрібні палиці на Говерлу?',
  createdBy: olena,
  createdAt: m1.createdAt,
  lastActivityAt: m4.createdAt,
  messageCount: NumberInt(4),
  lastMessages: [m2, m3, m4]
});

const n1 = { author: maria,  text: 'Скільки часу займає захід до озера Несамовите?', createdAt: ISODate('2026-09-11T10:00:00Z') };
const n2 = { author: andrii, text: 'Приблизно годину в один бік від основної стежки.', createdAt: ISODate('2026-09-11T10:40:00Z') };

upsert('discussions', {
  _id: D2,
  routeId: NumberInt(2), routeName: R2,
  title: 'Скільки часу йти до озера Несамовите?',
  createdBy: maria,
  createdAt: n1.createdAt,
  lastActivityAt: n2.createdAt,
  messageCount: NumberInt(2),
  lastMessages: [n1, n2]
});

upsert('discussionMessages', Object.assign({ _id: ObjectId('66f0c4000000000000000001'), discussionId: D1 }, m1));
upsert('discussionMessages', Object.assign({ _id: ObjectId('66f0c4000000000000000002'), discussionId: D1, replyTo: ObjectId('66f0c4000000000000000001') }, m2));
upsert('discussionMessages', Object.assign({ _id: ObjectId('66f0c4000000000000000003'), discussionId: D1, replyTo: ObjectId('66f0c4000000000000000001') }, m3));
upsert('discussionMessages', Object.assign({ _id: ObjectId('66f0c4000000000000000004'), discussionId: D1 }, m4));
upsert('discussionMessages', Object.assign({ _id: ObjectId('66f0c4000000000000000005'), discussionId: D2 }, n1));
upsert('discussionMessages', Object.assign({ _id: ObjectId('66f0c4000000000000000006'), discussionId: D2, replyTo: ObjectId('66f0c4000000000000000005') }, n2));

db.reviews.aggregate([
  { $group: {
      _id: '$routeId',
      avgRating: { $avg: '$rating' },
      count: { $sum: 1 },
      s1: { $sum: { $cond: [{ $eq: ['$rating', 1] }, 1, 0] } },
      s2: { $sum: { $cond: [{ $eq: ['$rating', 2] }, 1, 0] } },
      s3: { $sum: { $cond: [{ $eq: ['$rating', 3] }, 1, 0] } },
      s4: { $sum: { $cond: [{ $eq: ['$rating', 4] }, 1, 0] } },
      s5: { $sum: { $cond: [{ $eq: ['$rating', 5] }, 1, 0] } }
  } },
  { $project: {
      _id: 0,
      routeId: '$_id',
      avgRating: { $round: ['$avgRating', 2] },
      count: 1,
      distribution: { '1': '$s1', '2': '$s2', '3': '$s3', '4': '$s4', '5': '$s5' },
      updatedAt: '$$NOW'
  } },
  { $merge: { into: 'ratings', on: 'routeId', whenMatched: 'replace', whenNotMatched: 'insert' } }
]);

print('reviews: ' + db.reviews.countDocuments() +
      ', discussions: ' + db.discussions.countDocuments() +
      ', discussionMessages: ' + db.discussionMessages.countDocuments() +
      ', ratings: ' + db.ratings.countDocuments());
// очікується: reviews: 5, discussions: 2, discussionMessages: 6, ratings: 3
